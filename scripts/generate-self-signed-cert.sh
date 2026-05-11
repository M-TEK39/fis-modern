#!/usr/bin/env bash
# ============================================================================
# Generate a self-signed TLS certificate for FIS internal deployment.
# Run ONCE on the Ubuntu server before the first `docker compose up`.
# The cert is valid for 5 years and covers the server's IP + common hostnames.
# ============================================================================
set -euo pipefail

CERT_DIR="$(cd "$(dirname "$0")/.." && pwd)/docker/nginx/certs"
mkdir -p "$CERT_DIR"

# Detect the server's primary IP if not passed
SERVER_IP="${1:-$(hostname -I | awk '{print $1}')}"
SERVER_HOSTNAME="${2:-$(hostname)}"

echo "Generating self-signed cert for:"
echo "  IP:       $SERVER_IP"
echo "  Hostname: $SERVER_HOSTNAME"

# Subject Alternative Names — clients accept connections to any of these
cat > /tmp/fis-san.cnf <<EOF
[req]
distinguished_name = req_distinguished_name
x509_extensions = v3_req
prompt = no

[req_distinguished_name]
C  = ZA
ST = Gauteng
L  = Internal
O  = FIS
OU = Fleet Information System
CN = $SERVER_HOSTNAME

[v3_req]
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

[alt_names]
DNS.1 = $SERVER_HOSTNAME
DNS.2 = localhost
IP.1  = $SERVER_IP
IP.2  = 127.0.0.1
EOF

openssl req \
    -x509 \
    -nodes \
    -newkey rsa:4096 \
    -keyout "$CERT_DIR/fis.key" \
    -out "$CERT_DIR/fis.crt" \
    -days 1825 \
    -config /tmp/fis-san.cnf \
    -extensions v3_req

chmod 600 "$CERT_DIR/fis.key"
chmod 644 "$CERT_DIR/fis.crt"
rm /tmp/fis-san.cnf

echo ""
echo "✅ Cert generated:"
echo "  $CERT_DIR/fis.crt"
echo "  $CERT_DIR/fis.key"
echo ""
echo "Next steps:"
echo "  1. Trust this cert on each client workstation that needs to connect (import fis.crt as a trusted root)"
echo "  2. Bring up the stack: docker compose up -d"
echo "  3. Browse to https://$SERVER_IP/  (or https://$SERVER_HOSTNAME/)"
