#!/bin/bash

# Create the client cert directory in the current directory

subject="/CN=localhost/O=client/OU=$1"

mkdir -p ./bin/certs/client_$1

# Generate a new private key
openssl genpkey -algorithm RSA -out key.pem

# Generate a CSR using the private key
MSYS_NO_PATHCONV=1  openssl req -new -key key.pem -out request.csr -subj "$subject"

# Sign the CSR using the CA, creating a new certificate
openssl x509 -req -in request.csr -CA ./ca.crt -CAkey ./ca.key -CAcreateserial -out cert.crt

rm request.csr
rm ./bin/ca.srl

# Move the files to the correct location
mv key.pem ./bin/certs/client_$1/$1key.pem
mv cert.crt ./bin/certs/client_$1/$1cert.crt

unset MSYS_NO_PATHCONV

