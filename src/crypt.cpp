// Cryptographic methods and functions
// * OpenSSL will be used for the cryptographic methods
#include "_utils.hpp"
#include "user.hpp"

#include <cryptopp/cryptlib.h>
#include <cryptopp/pwdbased.h>
#include <cryptopp/hex.h>
#include <cryptopp/sha.h>
#include <cryptopp/rsa.h>
#include <cryptopp/aes.h>
#include <cryptopp/base64.h>
#include <cryptopp/files.h>
#include <cryptopp/osrng.h>
#include <string>

using namespace CryptoPP;

// region Hashing and Salting

std::string _rand::genSalt(size_t length) {
    AutoSeededRandomPool rng;
    SecByteBlock salt(length);

    rng.GenerateBlock(salt, salt.size());

    // Convert the binary salt into a hexadecimal string
    std::string hex_salt;
    HexEncoder encoder(new StringSink(hex_salt));
    encoder.Put(salt, salt.size());
    encoder.MessageEnd();

    return hex_salt;
}

std::pair<std::string, std::string> TLSS_U::hash(const std::string& data) {
    // gen a random salt
    std::string salt = _rand::genSalt(16);

    // hash the data with the salt
    byte key[SHA256::DIGESTSIZE];
    PKCS5_PBKDF2_HMAC<SHA256> pbkdf;
    pbkdf.DeriveKey(key, sizeof(key), 0, (byte*)data.data(), data.size(), (byte*)salt.data(), salt.size(), 10000);

    // convert the key to a hexadecimal string
    std::string hex_key;
    HexEncoder encoder(new StringSink(hex_key));
    encoder.Put(key, sizeof(key));
    encoder.MessageEnd();

    return std::make_pair(salt, hex_key);
}

bool TLSS_U::checkHash(const std::string& data, const std::string& salt, const std::string& hashed) {
    byte key[SHA256::DIGESTSIZE];
    PKCS5_PBKDF2_HMAC<SHA256> pbkdf;
    pbkdf.DeriveKey(key, sizeof(key), 0, (byte*)data.data(), data.size(), (byte*)salt.data(), salt.size(), 10000);

    // convert the key to a hexadecimal string
    std::string hex_key;
    HexEncoder encoder(new StringSink(hex_key));
    encoder.Put(key, sizeof(key));
    encoder.MessageEnd();

    return (hex_key == hashed);
}
// endregion


// region Key Generation and Encryption

std::pair<std::string, std::string> TLSS_U::genKeyPair() {
    AutoSeededRandomPool rng;
    
    // Generate private key
    RSA::PrivateKey privateKey;
    privateKey.GenerateRandomWithKeySize(rng, 2048);

    // Generate public key
    RSA::PublicKey publicKey(privateKey);

    // Convert the keys to strings
    std::string private_key;
    std::string public_key;

    // Convert the private and public keys to strings
    // * Sink objects convert keys (bytes) -> strings (base64)
    Base64Encoder privateSink(new StringSink(private_key));
    privateKey.DEREncode(privateSink);
    privateSink.MessageEnd();

    Base64Encoder publicSink(new StringSink(public_key));
    publicKey.DEREncode(publicSink);
    publicSink.MessageEnd();

    return std::make_pair(private_key, public_key);
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
