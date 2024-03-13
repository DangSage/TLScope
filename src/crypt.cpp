// Cryptographic methods and functions
// OpenSSL will be used for the cryptographic methods
#include "_utils.hpp"
#include "user.hpp"

#include <openssl/rand.h>
#include <string>
#include <sstream>
#include <iomanip>

using namespace TLSS_U;

std::string generate_salt(size_t length) {
    std::random_device rd;
    std::mt19937 gen(rd());
    std::uniform_int_distribution<> distrib(0, 255);

    std::vector<unsigned char> salt(length);
    for(auto& s : salt) {
        s = static_cast<unsigned char>(distrib(gen));
    }

    // Convert the binary salt into a hexadecimal string
    std::ostringstream oss;
    for(const auto& s : salt) {
        oss << std::hex << std::setw(2) << std::setfill('0') << static_cast<int>(s);
    }

    return oss.str();
}

std::pair<std::string, std::string> hash(const std::string& data) {
    // generate a random salt
    std::string salt = generate_salt(16);
}

bool check_hash(const std::string& data, const std::string& salt, const std::string& hash) {

}

std::pair<std::string, std::string> generate_key_pair() {

}

// encrypted_file_info = {
//     'encrypted_key': base64.b64encode(encrypted_key).decode(),
//     'nonce': base64.b64encode(cipher_aes.nonce).decode(),
//     'tag': base64.b64encode(tag).decode(),
//     'ciphertext': base64.b64encode(ciphertext).decode(),
// }
std::map<std::string, std::string> encrypt_file(
const std::string& file_path, const std::string& public_key) {

}

void decrypt_file(
std::map<std::string, std::string> encrypted_file_info) {
        
}
