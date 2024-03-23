// functionality with UDP portions of the network manager class

#include <iostream>
#include <string>
#include <memory>
#include <chrono>

#include <openssl/ssl.h>
#include <openssl/err.h>
#include <openssl/bio.h>

#include "network.hpp"
#include "_constants.hpp"

void NetManager::udpPing() {
    BIO *bio = BIO_new_dgram(createSocket(_ip, TLSS_C::PORT), BIO_NOCLOSE);
    SSL *ssl = SSL_new(_ctx);
    SSL_set_bio(ssl, bio, bio);

    while (_running) {
        std::string message = "ping";
        SSL_write(ssl, message.c_str(), message.size());
        std::this_thread::sleep_for(std::chrono::seconds(1));
    }

    SSL_shutdown(ssl);
    SSL_free(ssl);
}

void NetManager::udpReceive() {
    BIO *bio = BIO_new_dgram(createSocket(_ip, TLSS_C::PORT), BIO_NOCLOSE);
    SSL *ssl = SSL_new(_ctx);
    SSL_set_bio(ssl, bio, bio);

    if (DTLSv1_listen(ssl, NULL) <= 0) { exit(1); }

    while (_running) {
        char buffer[1024];
        int bytes = SSL_read(ssl, buffer, sizeof(buffer) - 1);
        if (bytes <= 0) {
            break;
        }

        buffer[bytes] = '\0';
        std::cout << "Received message: " << buffer << std::endl;
        SSL_write(ssl, buffer, bytes);
    }

    SSL_shutdown(ssl);
    SSL_free(ssl);
}