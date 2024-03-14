// Cryptographic methods and functions
// OpenSSL will be used for the cryptographic methods
#include "_utils.hpp"
#include "user.hpp"

#include <cryptopp/cryptlib.h>
#include <cryptopp/pwdbased.h>
#include <cryptopp/hex.h>
#include <cryptopp/sha.h>
#include <cryptopp/osrng.h>
#include <string>
#include <sstream>
#include <iomanip>

using CryptoPP::byte;

std::string _rand::genSalt(size_t length) {
    CryptoPP::AutoSeededRandomPool rng;
    CryptoPP::SecByteBlock salt(length);

    rng.GenerateBlock(salt, salt.size());

    // Convert the binary salt into a hexadecimal string
    std::string hex_salt;
    CryptoPP::HexEncoder encoder(new CryptoPP::StringSink(hex_salt));
    encoder.Put(salt, salt.size());
    encoder.MessageEnd();

    return hex_salt;
}

std::pair<std::string, std::string> TLSS_U::hash(const std::string& data) {
    // gen a random salt
    std::string salt = _rand::genSalt(16);

    // hash the data with the salt
    byte key[CryptoPP::SHA256::DIGESTSIZE];
    CryptoPP::PKCS5_PBKDF2_HMAC<CryptoPP::SHA256> pbkdf;
    pbkdf.DeriveKey(key, sizeof(key), 0, (byte*)data.data(), data.size(), (byte*)salt.data(), salt.size(), 10000);

    // convert the key to a hexadecimal string
    std::string hex_key;
    CryptoPP::HexEncoder encoder(new CryptoPP::StringSink(hex_key));
    encoder.Put(key, sizeof(key));
    encoder.MessageEnd();

    return std::make_pair(salt, hex_key);
}

bool TLSS_U::checkHash(const std::string& data, const std::string& salt, const std::string& hashed) {
    byte key[CryptoPP::SHA256::DIGESTSIZE];
    CryptoPP::PKCS5_PBKDF2_HMAC<CryptoPP::SHA256> pbkdf;
    pbkdf.DeriveKey(key, sizeof(key), 0, (byte*)data.data(), data.size(), (byte*)salt.data(), salt.size(), 10000);

    // convert the key to a hexadecimal string
    std::string hex_key;
    CryptoPP::HexEncoder encoder(new CryptoPP::StringSink(hex_key));
    encoder.Put(key, sizeof(key));
    encoder.MessageEnd();

    return (hex_key == hashed);
}

std::pair<std::string, std::string> TLSS_U::genKeyPair() {
    return std::make_pair("public_key", "private");
}

// encrypted_file_info = {
//     'encrypted_key': base64.b64encode(encrypted_key).decode(),
//     'nonce': base64.b64encode(cipher_aes.nonce).decode(),
//     'tag': base64.b64encode(tag).decode(),
//     'ciphertext': base64.b64encode(ciphertext).decode(),
// }
std::map<std::string, std::string> TLSS_U::encryptFile(
const std::string& file_path, const std::string& public_key) {
    return {};
}

void decrypt_file(
std::map<std::string, std::string> encrypted_file_info) {
    return;
}
