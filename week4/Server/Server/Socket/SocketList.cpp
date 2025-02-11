#include <iostream>
#include "SocketList.h"
#include "json.hpp"
#include "../Room.h"

using namespace std;
using json = nlohmann::json;

SocketList::SocketList()
{
	zerotime.tv_usec = 0;
	zerotime.tv_sec = 0;
	ConnectSocket = new ListenSocket;
}

SocketList::~SocketList()
{
	delete ConnectSocket;
	for (list<DataSocket*>::iterator itr = SerDatSockList.begin();
		itr != SerDatSockList.end(); itr++)
	{
		delete *itr;
	}
}

list<string> SocketList::Listening(Room* room,DataSocket **RecSocket)
{
	list<string> retDataList;

	// 初始化rset为NULL
	FD_ZERO(&rset);

	// 添加连接所用套接字
	FD_SET(ConnectSocket->GetSocket(), &rset);

	// 添加所有数据套接字链表
	for (list<DataSocket*>::iterator itr = SerDatSockList.begin(); itr != SerDatSockList.end(); itr++)
	{
		FD_SET((*itr)->GetSocket(), &rset);
	}

	// 查找在rset中是否有活动的套接字
	int i = select(0x7FFFFFFF, &rset, NULL, NULL, &zerotime);
	if (i > 0)
	{
		ListenSocket* listenSocket = dynamic_cast<ListenSocket*> (ConnectSocket);
		// case: 收到新的连接请求
		if (FD_ISSET(listenSocket->GetSocket(), &rset))
		{
			DataSocket *dsock = listenSocket->AcceptConnect();
			SerDatSockList.push_back(dsock); //接收的数据套接字入表
			*RecSocket = dsock;
			json j = {
				{"instruct",true},
				{"str","Connected"}
			};
			dsock->SendData(j.dump() + "#end#"); //返回给客户端Connected指令
		}
		// 遍历每个socket检查是否有活动
		for (list<DataSocket*>::iterator itr = SerDatSockList.begin(); itr != SerDatSockList.end(); itr++)
		{
			if (FD_ISSET((*itr)->GetSocket(), &rset))
			{
				string mes = (*itr)->ReceiveData();
				// case: 客户端关闭消息
				if (mes == "SocketClosed")
				{
					std::cout << "用户" << (*itr)->userName << "的客户端关闭了" << std::endl;
					if (room->findName((*itr)->userName)) {
						room->nameInRoom.remove((*itr)->userName);
						room->refreshNames();
					}
					delete (*itr);
					itr = SerDatSockList.erase(itr);
					if (itr == SerDatSockList.end())
						break;
					continue;
				}
				// case: 普通消息
				else
				{
					retDataList.push_back(mes + "#end#"); //消息入表
					*RecSocket = *itr;
				}
			}
		}
	}
	return retDataList;
}

void SocketList::SendToAllClient(list<string> StrList)
{
	for (list<string>::iterator sItr = StrList.begin(); sItr != StrList.end(); sItr++)
	{
		SendToAllClient(*sItr);
	}
}

void SocketList::SendToAllClient(string str)
{
	for (list<DataSocket*>::iterator itr = SerDatSockList.begin(); itr != SerDatSockList.end(); itr++)
	{
		(*itr)->SendData(str);
	}
}