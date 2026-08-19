#!/bin/bash
set -e

echo "=== 🚀 1. Updating System Packages ==="
apt-get update -y
apt-get install -y wget curl git nginx certbot python3-certbot-nginx xz-utils libfontconfig1

echo "=== ⚡ 2. Installing .NET 8 Runtime ==="
if ! command -v dotnet &> /dev/null; then
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
    chmod +x dotnet-install.sh
    ./dotnet-install.sh --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet
    ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
fi

echo "=== 📄 3. Installing Typst CLI ==="
if ! command -v typst &> /dev/null; then
    curl -L https://github.com/typst/typst/releases/download/v0.11.0/typst-x86_64-unknown-linux-musl.tar.xz | tar -xJ
    mv typst-x86_64-unknown-linux-musl/typst /usr/local/bin/
    chmod +x /usr/local/bin/typst
fi

echo "=== 📁 4. Creating Application Directory ==="
mkdir -p /var/www/fenixlegalos

echo "=== ⚙️ 5. Setting up Systemd Service ==="
cat << 'EOF' > /etc/systemd/system/fenix.service
[Unit]
Description=Fenix Legal OS Web Application
After=network.target

[Service]
WorkingDirectory=/var/www/fenixlegalos
ExecStart=/usr/bin/dotnet /var/www/fenixlegalos/FenixLegalOs.dll
Restart=always
RestartSec=10
SyslogIdentifier=fenix-app
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=PORT=5000

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload

echo "=== 🌐 6. Configuring Nginx Reverse Proxy ==="
cat << 'EOF' > /etc/nginx/sites-available/fenixlaw
server {
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name _;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF

ln -sf /etc/nginx/sites-available/fenixlaw /etc/nginx/sites-enabled/default
systemctl reload nginx

echo "=== ✅ VPS SETUP COMPLETE ==="
