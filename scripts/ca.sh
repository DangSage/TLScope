#!/bin/bash

subject="/C=US/ST=MA/L=Lowell/O=SecureDrop/CN=localhost"

# Generate a new RSA private key
openssl genrsa -out ca.key 2048

# Generate a new X.509 certificate
MSYS_NO_PATHCONV=1 openssl req -new -x509 -days 365 -key ca.key -out ca.crt -subj "$subject"

# delete msys path conversion variable
unset MSYS_NO_PATHCONV

# wait for the user to press enter
read -p "Press enter to continue"