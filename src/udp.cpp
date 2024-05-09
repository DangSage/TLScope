// functionality with UDP portions of the network manager class

#include <iostream>
#include <string>
#include <memory>
#include <chrono>
#include <thread>
#include <future>

#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <unistd.h>

#include "network.hpp"
#include "_constants.hpp"
#include "_utils.hpp"
#include "user.hpp"

void NetManager::udpHandler() {
    std::string message = "ping";

    int len, n;
    len = sizeof(cliaddr);
    char buffer[1024];
    std::string receivedMessage;
    receivedMessage.reserve(1024);
    int port = TLSS_C::PORT+10;
    int endPort = TLSS_C::PORT+20;

    while (_running) {
        // Create a future for the send operation
        auto send_future = std::async(std::launch::async, [&]() {
            servaddr.sin_addr.s_addr = INADDR_BROADCAST;
            port > endPort ? port = TLSS_C::PORT+10 : port++;
            servaddr.sin_port = htons(port);
            int bytesSent = sendto(_uSocket, (const char *)message.c_str(), message.size(),
                MSG_CONFIRM, (const struct sockaddr *) &servaddr, 
                sizeof(servaddr));
            if (bytesSent == -1) {
                perror("sendto error");
            }
        });

        // Create a future for the receive operation
        auto recv_future = std::async(std::launch::async, [&]() {
            servaddr.sin_addr.s_addr = INADDR_ANY;
            n = recvfrom(_uSocket, buffer, sizeof(buffer) - 1, MSG_WAITALL,
            (struct sockaddr *) &cliaddr, (socklen_t*)&len);
            if (n >= 0) {
                buffer[n] = '\0';
                receivedMessage.assign(buffer);
            } else {
                if (errno == EWOULDBLOCK) {
                    // timeout, no data received
                    return;
                } else {
                    perror("recvfrom error");
                    _running = false;
                }
            }
        });

        // Wait for both futures to complete
        send_future.get();
        recv_future.get();

        if (receivedMessage.find("pong:") != std::string::npos) {
            std::string token = receivedMessage.substr(5);

            if (_users.find(token) == _users.end()) {
                USER user;
                user.name = "\"User "+std::to_string(_users.size())+"\"";
                user.IPP = std::string(
                    inet_ntoa(cliaddr.sin_addr)) + ":" + std::to_string(ntohs(cliaddr.sin_port));
                _users[token] = std::make_shared<USER>(user);
            }
            continue;
        }
        if (ntohs(cliaddr.sin_port) == _uPort) {
            continue;
        }
        // Always send a "pong" response back to the client
        std::string response = "pong:" + _token;
        sendto(_uSocket, (const char *)response.c_str(), response.size(),
            MSG_CONFIRM, (const struct sockaddr *) &cliaddr, len);
    }
    close(_uSocket);
}

