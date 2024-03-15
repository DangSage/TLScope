// Network header: network class and methods
// * will contain all network related classes and methods
#ifndef _TLSS_NETWORK_HPP_4204
#define _TLSS_NETWORK_HPP_4204

#include <iostream>
#include <string>
#include <memory>
#include <openssl/ssl.h>
#include <vector>
#include <map>

// forward declarations
class USER;

// network class
// * will contain all network related classes and methods
// * needs to be able to send and receive data
class NETWORK_MANAGER {
public:
    NETWORK_MANAGER() {
    //  session_token()
    // gen_certificate(gl.USER_EMAIL)
    // write_session_token()
    // ng.bcast_port, ng.tcp_listen = port_manager(ng.bcast_port, ng.tcp_listen)

    // # broadcast socket setup (multicast)
    // broadcast_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    // broadcast_socket.bind(("", ng.bcast_port))
    

    // udp_listener = Thread(
    //     target=broadcast_listen,
    //     name="broadcast_listener",
    //     args=(broadcast_socket,)
    // )

    // udp_sender = Thread(
    //     target=broadcast_send,
    //     name="broadcast_sender",
    //     args=(ng.bcast_port,)
    // )

    // tcp_listener = Thread(
    //     target=tcp_listen,
    //     name="tcp_listener",
    //     args=(ng.tcp_listen,)
    // )
    }

    ~NETWORK_MANAGER () {
        // broadcast_socket.close()
    }

private:
    // my ip address
    std::string ip;
    // session token
    std::string token;
    // list of online users on the network
    std::map<std::string, USER> onlineUsers;
    // list of trusted users on the network
    std::map<std::string, USER> trustedUsers;
    // list of contact requests received by the client
    std::vector<std::string> contactRequestsReceived;
    // list of pending contact requests sent by the client
    std::vector<std::string> pendingContactRequests;
    // list of ports to ignore when broadcasting
    std::vector<int> portsToIgnore;
};

#endif // _TLSS_NETWORK_HPP_4204
