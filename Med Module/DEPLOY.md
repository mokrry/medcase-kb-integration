# Production Deploy

Target setup:

- VPS: Ubuntu 20.04 LTS
- Domain: `med-module.ru`
- Public IP: `130.12.47.15`
- Docker Compose runs `postgres`, `backend`, `frontend`
- Host nginx proxies `https://med-module.ru` to `127.0.0.1:8080`

## 1. DNS

In REG.RU DNS zone:

```text
A  @    130.12.47.15
A  www  130.12.47.15
```

Check:

```powershell
nslookup med-module.ru
nslookup www.med-module.ru
```

## 2. Server Packages

On the VPS:

```bash
apt update && apt upgrade -y
apt install -y ca-certificates curl gnupg git nginx
```

Install Docker for Ubuntu 20.04:

```bash
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu focal stable" > /etc/apt/sources.list.d/docker.list
apt update
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Check:

```bash
docker --version
docker compose version
systemctl status docker --no-pager
```

## 3. Upload Project

Create app directory:

```bash
mkdir -p /opt/med-module
```

Upload project files to `/opt/med-module` using `git clone`, `scp`, or CI/CD.

The directory must contain:

```text
backend/
frontend/
docker-compose.prod.yml
.env
```

## 4. Environment

Create `/opt/med-module/.env` from `.env.production.example`:

```bash
cd /opt/med-module
cp .env.production.example .env
nano .env
```

Set strong values for:

```text
POSTGRES_PASSWORD
JWT_SECRET
SEED_ADMIN_EMAIL
SEED_ADMIN_PASSWORD
CHATGPT_PROXY_API_KEY
GEMINI_PROXY_API_KEY
```

Do not commit `.env`.

## 5. Run App

```bash
cd /opt/med-module
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Check containers:

```bash
docker compose -f docker-compose.prod.yml --env-file .env ps
docker compose -f docker-compose.prod.yml --env-file .env logs -f backend
```

Local server check:

```bash
curl -I http://127.0.0.1:8080
```

## 6. Host Nginx

Create nginx config:

```bash
nano /etc/nginx/sites-available/med-module.ru
```

Paste:

```nginx
server {
    listen 80;
    server_name med-module.ru www.med-module.ru;

    client_max_body_size 20m;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Authorization $http_authorization;
    }
}
```

Enable:

```bash
ln -s /etc/nginx/sites-available/med-module.ru /etc/nginx/sites-enabled/med-module.ru
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx
```

## 7. HTTPS

After DNS points to the VPS:

```bash
apt install -y certbot python3-certbot-nginx
certbot --nginx -d med-module.ru -d www.med-module.ru
```

## 8. Useful Commands

Restart app:

```bash
cd /opt/med-module
docker compose -f docker-compose.prod.yml --env-file .env restart
```

Update app after uploading new files:

```bash
cd /opt/med-module
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

View logs:

```bash
docker compose -f docker-compose.prod.yml --env-file .env logs -f
```

Stop app:

```bash
docker compose -f docker-compose.prod.yml --env-file .env down
```

## 9. GitHub Actions Auto Deploy

The repository contains a workflow:

```text
.github/workflows/deploy-production.yml
```

On every push to `main`, it:

1. Creates an archive from `Med Module/backend`, `Med Module/frontend`, `docker-compose.prod.yml`.
2. Uploads it to the VPS through SSH.
3. Updates `/opt/med-module` while preserving `/opt/med-module/.env`.
4. Runs:

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Create an SSH key for GitHub Actions on your local machine:

```powershell
ssh-keygen -t ed25519 -C "github-actions-med-module" -f "$env:USERPROFILE\.ssh\med_module_github_actions"
```

Add the public key to the VPS:

```powershell
type "$env:USERPROFILE\.ssh\med_module_github_actions.pub"
ssh root@130.12.47.15
```

On the VPS:

```bash
mkdir -p ~/.ssh
chmod 700 ~/.ssh
nano ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

Paste the public key into `authorized_keys`.

In GitHub repository settings, add secrets:

```text
VPS_HOST=130.12.47.15
VPS_USER=root
VPS_PORT=22
VPS_SSH_KEY=<private key from C:\Users\<you>\.ssh\med_module_github_actions>
```

Get the private key content:

```powershell
Get-Content -Raw "$env:USERPROFILE\.ssh\med_module_github_actions"
```

Do not add `.env` to GitHub. Production secrets stay only in `/opt/med-module/.env`.
