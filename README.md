# Frota Online API

Marketplace de fretes para motoristas de veículos de carga. Monolito modular em **.NET 9**, com PostgreSQL e EF Core.

## Requisitos

- SDK do .NET 9
- Docker (para o PostgreSQL local)

## Como rodar

```powershell
.\setup.ps1                      # solução, Postgres (docker), build e testes
```

Só na primeira vez, crie o esquema do banco. Pule a linha `add` se a pasta `src/FrotaOnline.Infrastructure/Persistence/Migrations` já existir.

```powershell
dotnet ef migrations add InitialCreate -p src/FrotaOnline.Infrastructure -s src/FrotaOnline.Api -o Persistence/Migrations
dotnet ef database update          -p src/FrotaOnline.Infrastructure -s src/FrotaOnline.Api
```

Depois, inicie a API (o Visual Studio faz o mesmo com o perfil `FrotaOnline.Api`):

```powershell
dotnet run --project src/FrotaOnline.Api
```

Em Development, a rota `/` redireciona para o guia da documentação.

## Documentação da API

Disponível só em Development, com a API rodando (as portas vêm de `src/FrotaOnline.Api/Properties/launchSettings.json`):

- `/docs` – guia por jornada, com requisições de teste
- `/swagger` – Swagger UI (use **Authorize** com o `accessToken` de `/api/v1/auth/login`)
- `/openapi/v1.json` – contrato OpenAPI

## Configuração por ambiente

O ambiente vem de `ASPNETCORE_ENVIRONMENT` (o Visual Studio define `Development` no `launchSettings.json`).
As configurações são aplicadas em camadas: `appsettings.json`, depois `appsettings.{Environment}.json`, depois variáveis de ambiente.

| Ambiente | Banco | Onde fica a string de conexão |
| --- | --- | --- |
| Development | `frotaonline` no Postgres local do Docker | `appsettings.Development.json` (só credenciais locais) |
| Staging | servidor e banco próprios | variável de ambiente `ConnectionStrings__Default` ou gerenciador de segredos |
| Production | servidor e banco próprios | variável de ambiente `ConnectionStrings__Default` ou gerenciador de segredos |

- O `appsettings.json` deixa `ConnectionStrings:Default` vazia de propósito. A API se recusa a iniciar em um ambiente que não tenha uma.
- `Jwt:SecretKey` segue a mesma regra: no mínimo 32 caracteres, diferente em cada ambiente e definida com `Jwt__SecretKey` fora do Development.
- Staging e Production usam emissor e audiência de JWT próprios, então um token de um ambiente não vale no outro.
- Para outro ambiente, rode `dotnet ef database update ...` com `ASPNETCORE_ENVIRONMENT` e `ConnectionStrings__Default` definidos.

### Variáveis de ambiente para subir em Staging e Production

Em variável de ambiente, o `:` do JSON vira `__` (dois sublinhados): `Jwt:SecretKey` é `Jwt__SecretKey`. Variáveis de ambiente vencem os arquivos `appsettings`.

**Obrigatórias** (a API não inicia sem elas):

| Variável | Exemplo | Observação |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` ou `Staging` | Carrega `appsettings.{Environment}.json`. Fora de `Development` a documentação (`/docs`, `/swagger`, `/openapi`) e o seed do admin ficam desligados. |
| `ConnectionStrings__Default` | `Host=...;Port=5432;Database=frotaonline;Username=...;Password=...` | Um banco por ambiente. No Cloud SQL pelo Cloud Run use o socket: `Host=/cloudsql/<projeto>:<regiao>:<instancia>;Database=frotaonline;Username=...;Password=...`. Guarde no Secret Manager. |
| `Jwt__SecretKey` | 32+ caracteres aleatórios | Diferente em cada ambiente e nunca igual à de desenvolvimento (que está no histórico do Git: gere outra e descarte a antiga). Trocar a chave invalida todos os tokens emitidos. Secret Manager. |

**Necessárias para o que já está implementado em produção:**

| Variável | Valor | Para quê |
| --- | --- | --- |
| `Push__Provider` | `Firebase` (padrão `Logging`, que só escreve no log) | Liga o envio real de push pelo FCM. |
| `GOOGLE_APPLICATION_CREDENTIALS` | caminho do JSON da conta de serviço | Só fora do GCP ou em máquina local. No Cloud Run/GKE não defina: vale a conta de serviço do serviço, com o papel de administrador da API do Firebase Cloud Messaging. |
| `Jobs__Audience` | URL do serviço no Cloud Run | Audiência do token OIDC que o Cloud Scheduler envia. |
| `Jobs__ServiceAccountEmail` | `scheduler@<projeto>.iam.gserviceaccount.com` | Única conta aceita em `/internal/jobs/*`. Vazio = tudo recusado. |
| `Jobs__RunInProcess` | `false` (padrão) | Deixe `false` em produção; `true` roda os jobs dentro da API (só desenvolvimento ou instância única). |
| `Payments__Provider` | `Asaas` (padrão `None`) | Liga assinaturas, Pix com split e Pix da taxa. Com `None` esses endpoints respondem `409 payments.not_configured`. |
| `Payments__Asaas__ApiKey` | chave de API da conta Asaas da plataforma | Obrigatória com `Asaas` (a API não inicia sem ela). Uma chave para o sandbox e outra para produção. Secret Manager. |
| `Payments__Asaas__BaseUrl` | `https://api.asaas.com/v3` em produção | O padrão do projeto é o sandbox (`https://api-sandbox.asaas.com/v3`). A documentação do Asaas mostra dois endereços de sandbox; use o que aparece na sua conta. |
| `Payments__Asaas__WebhookToken` | 32 a 255 caracteres aleatórios, sem espaços | O mesmo valor que você cadastra no webhook do Asaas (campo "token de autenticação"). Sem ele, o webhook recusa tudo (401). Secret Manager. |
| `Payments__WalletProvider` | `MercadoPago` (padrão `None`) | Liga o pagamento com carteira (Apple Pay) e a conexão da conta do motorista. Com `None`, `409 payments.not_configured`. Com `MercadoPago`, os cinco valores abaixo são obrigatórios (a API não inicia sem eles). |
| `Payments__MercadoPago__ClientId` / `ClientSecret` | credenciais da aplicação em "Suas integrações" do Mercado Pago | `ClientSecret` no Secret Manager. |
| `Payments__MercadoPago__RedirectUri` | `https://<sua-api>/api/v1/callbacks/mercadopago/oauth` | Cadastre o mesmo endereço na aplicação do Mercado Pago. |
| `Payments__MercadoPago__WebhookSecret` | "chave secreta" do webhook, no painel | Valida o cabeçalho `x-signature`. Sem ela o webhook recusa tudo (401). Secret Manager. |
| `Payments__MercadoPago__TokenEncryptionKey` | base64 de 32 bytes aleatórios (`openssl rand -base64 32`) | Criptografa (AES-256-GCM) os tokens dos motoristas no banco. Perdê-la desconecta todos os motoristas. Secret Manager. |
| `Payments__MercadoPago__AppReturnUrl` / `NotificationUrl` | opcionais | `AppReturnUrl`: deep link do app para onde o navegador volta depois de o motorista conectar a conta (recebe `?status=ok` ou `?status=error`). `NotificationUrl`: endereço do webhook enviado em cada pagamento (`/api/v1/webhooks/mercadopago`), se você não o cadastrar no painel. |
| `Subscriptions__RequiredToPropose` | `false` (padrão) | `true` exige assinatura ativa para enviar propostas, exceto o primeiro frete, que é grátis. Deixe `false` até o lançamento dos planos. |
| `Storage__Provider` | `Gcs` (padrão `None`) | Liga o upload real por URL pré-assinada no Google Cloud Storage. Com `None` o upload fica desligado (`409 storage.not_configured`). |
| `Storage__Bucket` | nome do bucket | Obrigatório com `Gcs`. Bucket privado, sem acesso público. A conta de serviço precisa de `roles/storage.objectAdmin` no bucket e de `roles/iam.serviceAccountTokenCreator` em si mesma (assinatura V4 sem arquivo de chave). |
| `Routing__BaseUrl` | URL do seu OSRM | O servidor público padrão (`router.project-osrm.org`) é só para teste e não tem garantia de disponibilidade: suba o seu (imagem `osrm/osrm-backend` com o mapa do Brasil). |

**Opcionais** (têm padrão no `appsettings.json`):

| Variável | Padrão | Para quê |
| --- | --- | --- |
| `Jwt__Issuer` / `Jwt__Audience` | `FrotaOnline` / `FrotaOnline.Clients` (Staging: `FrotaOnline.Staging` / `FrotaOnline.Staging.Clients`) | Mantenha diferentes por ambiente para um token de um não valer no outro. |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | `15` / `30` | Duração das sessões. |
| `Routing__TimeoutSeconds` | `10` | Passou disso, o frete segue sem a linha da rota. |
| `Logging__LogLevel__Default` | `Information` (Production: `Warning`) | Nível de log. |
| `AllowedHosts` | `*` | Restrinja ao domínio da API em produção. |
| `PORT` / `ASPNETCORE_URLS` | `8080` no contêiner .NET | O Cloud Run injeta `PORT`; não precisa mexer. |

**Só em Development** (ignoradas nos outros ambientes): `Seed__Admin__FullName`, `Seed__Admin__Email` e `Seed__Admin__Password`. Como o seed não roda em Staging nem Production, o primeiro admin desses ambientes precisa ser criado à parte (por exemplo um script SQL ou um comando único, ainda não existe na API). Planeje isso antes do primeiro deploy.

> **Lembrete: CORS ainda não está implementado.** A API não tem nenhuma política de CORS (`AddCors` / `UseCors`). Um app mobile nativo não é afetado, mas qualquer front-end web (inclusive a versão Web do app, `platform: Web`, e a página de docs servida de outro domínio) terá as chamadas bloqueadas pelo navegador. Antes de publicar um front-end web: definir uma política nomeada que libere só as origens de cada ambiente (por exemplo `Cors__AllowedOrigins__0=https://app.seudominio.com.br`, nunca `*` junto de `Authorization`), permitir os métodos e cabeçalhos usados (`Authorization`, `Content-Type`) e chamar `app.UseCors(...)` depois do `UseForwardedHeaders` e antes de `UseAuthentication`. As rotas `/internal/jobs/*` não precisam de CORS, pois são chamadas pelo Cloud Scheduler.

Checklist de deploy: (0) criar a conta Asaas de produção, a chave Pix e o webhook (veja "Conta Asaas"); (1) criar o banco e aplicar as migrations com `ConnectionStrings__Default` do ambiente; (2) definir os segredos acima; (3) publicar a API e criar os dois jobs do Cloud Scheduler (veja "Passo a passo no GCP"); (4) publicar o termo de uso inicial e criar o primeiro admin; (5) conferir `GET /health`; (6) configurar o CORS, se houver front-end web.

Para manter a sua chave JWT de desenvolvimento fora do repositório:

```powershell
dotnet user-secrets init --project src/FrotaOnline.Api
dotnet user-secrets set "Jwt:SecretKey" "<no mínimo 32 caracteres aleatórios>" --project src/FrotaOnline.Api
```

## Jobs em produção (Cloud Scheduler)

No Cloud Run um serviço em segundo plano não é confiável (a instância pode escalar a zero, ou várias rodarem juntas). Por isso os jobs são endpoints internos, chamados pelo Cloud Scheduler a cada minuto:

| Rota | O que faz |
| --- | --- |
| `POST /internal/jobs/expire` | Expira fretes e propostas fora da janela e avisa as pessoas. Resposta `{ freights, proposals }`. |
| `POST /internal/jobs/dispatch-notifications` | Envia um lote de até 100 pushes pendentes pelo Firebase (FCM). Resposta `{ settled }`. Pode rodar em várias instâncias ao mesmo tempo. |

As rotas não aparecem na documentação e só aceitam um token OIDC do Google emitido para a conta de serviço do Scheduler, com a audiência igual a `Jobs__Audience`. Se `Jobs__ServiceAccountEmail` estiver vazio, toda chamada é recusada (401/403).

Sem esses jobs a API funciona, mas fretes e propostas não expiram e nenhum push é enviado (as notificações continuam na caixa de entrada do app).

### Passo a passo no GCP (nesta ordem)

Troque `PROJECT`, `REGION` (por exemplo `southamerica-east1`) e `SERVICE` pelos seus valores.

**1. Habilitar as APIs**

```bash
gcloud services enable run.googleapis.com cloudscheduler.googleapis.com secretmanager.googleapis.com \
  sqladmin.googleapis.com fcm.googleapis.com storage.googleapis.com --project PROJECT
```

**2. Banco (Cloud SQL para PostgreSQL)**: crie a instância e o banco `frotaonline`. Aplique as migrations de um computador com acesso ao banco (por exemplo pelo Cloud SQL Auth Proxy), com `ASPNETCORE_ENVIRONMENT` e `ConnectionStrings__Default` do ambiente:

```bash
dotnet ef database update -p src/FrotaOnline.Infrastructure -s src/FrotaOnline.Api
```

**3. Segredos (Secret Manager)**: crie um segredo para cada valor sensível da tabela "Obrigatórias" e "Necessárias" acima (`ConnectionStrings__Default`, `Jwt__SecretKey`, `Payments__Asaas__ApiKey`, `Payments__Asaas__WebhookToken` e os do Mercado Pago):

```bash
printf '%s' 'valor' | gcloud secrets create jwt-secret-key --data-file=- --project PROJECT
```

**4. Conta de serviço da API**: a identidade com que o serviço roda. Ela precisa ler os segredos, falar com o Cloud SQL, enviar push e assinar URLs de upload:

```bash
gcloud iam service-accounts create frota-api --project PROJECT
SA=frota-api@PROJECT.iam.gserviceaccount.com
for ROLE in roles/secretmanager.secretAccessor roles/cloudsql.client roles/firebasecloudmessaging.admin; do
  gcloud projects add-iam-policy-binding PROJECT --member serviceAccount:$SA --role $ROLE
done
# Upload de arquivos (Storage__Provider=Gcs): acesso ao bucket e permissão de assinar com a própria conta
gcloud storage buckets add-iam-policy-binding gs://BUCKET --member serviceAccount:$SA --role roles/storage.objectAdmin
gcloud iam service-accounts add-iam-policy-binding $SA --member serviceAccount:$SA --role roles/iam.serviceAccountTokenCreator
```

**5. Publicar a API no Cloud Run**: publique a imagem do contêiner da API e crie o serviço. O repositório não tem Dockerfile; gere a imagem do jeito que você preferir e informe-a em `--image`. O serviço precisa aceitar chamadas sem login do Google (`--allow-unauthenticated`): o app e o Scheduler se autenticam na própria API. Os segredos entram como variáveis de ambiente. Push, pagamentos, upload e rota são ligados depois, um a um, em "Serviços externos" (a API não inicia com `Payments__Provider=Asaas` sem a chave do Asaas, por exemplo).

```bash
gcloud run deploy SERVICE --project PROJECT --region REGION --image IMAGE \
  --service-account $SA --allow-unauthenticated --min-instances 0 \
  --set-env-vars ASPNETCORE_ENVIRONMENT=Production \
  --set-secrets ConnectionStrings__Default=db-connection:latest,Jwt__SecretKey=jwt-secret-key:latest
```

Anote a URL do serviço (`https://SERVICE-<hash>.REGION.run.app`):

```bash
gcloud run services describe SERVICE --project PROJECT --region REGION --format 'value(status.url)'
```

**6. Conta de serviço do Scheduler**: outra conta, que só serve para chamar a API:

```bash
gcloud iam service-accounts create frota-scheduler --project PROJECT
SCHED=frota-scheduler@PROJECT.iam.gserviceaccount.com
```

**7. Dizer à API quem pode chamar os jobs** (precisa da URL do passo 5, por isso vem depois do primeiro deploy):

```bash
gcloud run services update SERVICE --project PROJECT --region REGION \
  --update-env-vars Jobs__Audience=https://SERVICE-<hash>.REGION.run.app,Jobs__ServiceAccountEmail=$SCHED
```

**8. Criar os dois jobs do Cloud Scheduler**: a audiência é a URL do serviço, sem caminho e sem barra no final, e deve ser igual a `Jobs__Audience`.

```bash
URL=https://SERVICE-<hash>.REGION.run.app

gcloud scheduler jobs create http frota-expire \
  --project PROJECT --location REGION --schedule '* * * * *' --time-zone 'America/Sao_Paulo' \
  --http-method POST --uri $URL/internal/jobs/expire \
  --oidc-service-account-email $SCHED --oidc-token-audience $URL

gcloud scheduler jobs create http frota-dispatch-notifications \
  --project PROJECT --location REGION --schedule '* * * * *' --time-zone 'America/Sao_Paulo' \
  --http-method POST --uri $URL/internal/jobs/dispatch-notifications \
  --oidc-service-account-email $SCHED --oidc-token-audience $URL
```

A frequência mínima do Scheduler é 1 minuto, então um push pode demorar até 1 minuto. A API continua recusando propostas atrasadas na hora, então a expiração não depende do job.

**9. Conferir**

```bash
# roda cada job agora e olha o resultado nos logs do Cloud Run (deve ser 200)
gcloud scheduler jobs run frota-expire --project PROJECT --location REGION
gcloud scheduler jobs run frota-dispatch-notifications --project PROJECT --location REGION
gcloud logging read 'resource.type=cloud_run_revision AND httpRequest.requestUrl:"/internal/jobs"' \
  --project PROJECT --limit 10 --format 'value(httpRequest.status,httpRequest.requestUrl)'

# sem token a rota deve recusar
curl -i -X POST $URL/internal/jobs/expire      # 401

curl $URL/health                                # {"status":"ok"}
```

Se os jobs responderem 401 ou 403, confira, nesta ordem: se `Jobs__Audience` é exatamente a URL usada em `--oidc-token-audience`, se `Jobs__ServiceAccountEmail` é a conta do passo 6 e se o job foi criado com essa conta. Para pausar um job: `gcloud scheduler jobs pause frota-dispatch-notifications --project PROJECT --location REGION`.

Em desenvolvimento não precisa de Scheduler: `Jobs:RunInProcess` é `true` e os jobs rodam dentro da API (expiração a cada 30 s, push a cada 5 s). Não ligue isso em produção com mais de uma instância.

## Serviços externos: como configurar cada um

A API fala com serviços de fora. Cada um é opcional e vem desligado por padrão; sem ele, o endpoint correspondente responde `409 ...not_configured` (pagamentos, upload) ou a função cai num modo simples (push só no log, rota sem linha no mapa). Ordem sugerida, depois do deploy do passo 5 acima, porque vários precisam da URL da API:

| # | Serviço | Para quê | Liga com |
| --- | --- | --- | --- |
| 1 | Firebase Cloud Messaging | Push no celular | `Push__Provider=Firebase` |
| 2 | Google Cloud Storage | Documentos e fotos do motorista | `Storage__Provider=Gcs` e `Storage__Bucket` |
| 3 | OSRM | Distância, tempo e linha da rota | `Routing__BaseUrl` |
| 4 | Asaas | Assinatura, Pix com split, Pix da taxa e cartão | `Payments__Provider=Asaas` |
| 5 | Mercado Pago | Apple Pay e carteiras | `Payments__WalletProvider=MercadoPago` |
| 6 | Primeiro admin e termos de uso | Aprovar motoristas e aceite do usuário | SQL e `POST /api/v1/admin/terms` |

Para trocar um valor depois: `gcloud run services update SERVICE --project PROJECT --region REGION --update-env-vars CHAVE=valor` (ou `--update-secrets` para segredo). Cada troca cria uma revisão nova do serviço.

### 1. Firebase Cloud Messaging (push)

1. No [console do Firebase](https://console.firebase.google.com), adicione o projeto **existente do GCP** (o mesmo `PROJECT`), assim a API usa a conta de serviço dela e não precisa de arquivo de chave.
2. A conta de serviço da API precisa de `roles/firebasecloudmessaging.admin` (passo 4 do GCP). Fora do GCP, aponte `GOOGLE_APPLICATION_CREDENTIALS` para o JSON de uma conta de serviço com esse papel.
3. Para iOS, em *Configurações do projeto > Cloud Messaging*, envie a chave APNs (`.p8`) da sua conta Apple Developer. Android não precisa de mais nada nesta etapa.
4. No app nativo, registre o app (iOS e Android) no Firebase e use o token que ele gera: o app chama `PUT /api/v1/devices` depois de cada login.
5. Ligue: `--update-env-vars Push__Provider=Firebase`.

Teste: faça login no app, crie uma proposta para um frete seu com outra conta e espere até 1 minuto (o job `dispatch-notifications`). Sem o push, a notificação continua na caixa de entrada do app.

### 2. Google Cloud Storage (upload de documentos e fotos)

```bash
gcloud storage buckets create gs://BUCKET --project PROJECT --location REGION --uniform-bucket-level-access --public-access-prevention
```

O bucket é privado: a API gera URLs temporárias para enviar (15 min) e para o admin ler (10 min). Os papéis da conta de serviço (`storage.objectAdmin` no bucket e `serviceAccountTokenCreator` nela mesma) já estão no passo 4. Se houver app web, aplique CORS no bucket (app nativo não precisa):

```bash
cat > cors.json <<'JSON'
[{ "origin": ["https://app.seudominio.com.br"], "method": ["PUT"],
   "responseHeader": ["Content-Type", "x-goog-content-length-range"], "maxAgeSeconds": 3600 }]
JSON
gcloud storage buckets update gs://BUCKET --cors-file=cors.json
```

Ligue: `--update-env-vars Storage__Provider=Gcs,Storage__Bucket=BUCKET`. Sem isso o upload fica desligado (`409 storage.not_configured`).

### 3. OSRM (rota no mapa)

O servidor público padrão (`router.project-osrm.org`) é só para teste e pode falhar ou demorar. Quando o OSRM não responde a tempo, a API **não dá erro**: devolve distância e tempo vazios e o app mostra uma linha reta. Para produção, suba o seu com o mapa do Brasil. O preparo do mapa usa muita memória; faça-o numa máquina maior e depois sirva em uma menor.

```bash
mkdir osrm && cd osrm
curl -LO https://download.geofabrik.de/south-america/brazil-latest.osm.pbf
docker run -t -v "$PWD:/data" osrm/osrm-backend osrm-extract -p /opt/car.lua /data/brazil-latest.osm.pbf
docker run -t -v "$PWD:/data" osrm/osrm-backend osrm-partition /data/brazil-latest.osrm
docker run -t -v "$PWD:/data" osrm/osrm-backend osrm-customize /data/brazil-latest.osrm
docker run -d --restart always -p 5000:5000 -v "$PWD:/data" osrm/osrm-backend osrm-routed --algorithm mld /data/brazil-latest.osrm
```

Teste: `curl 'http://localhost:5000/route/v1/driving/-46.65,-23.56;-46.63,-23.55?overview=false'` deve devolver `"code":"Ok"`. Hospede numa VM (Compute Engine) ou outro lugar com endereço fixo, acessível pela API (use HTTPS se sair da rede privada) e ligue: `--update-env-vars Routing__BaseUrl=https://SEU-OSRM`. Se o servidor for lento, aumente `Routing__TimeoutSeconds` (padrão 10). Atualize o mapa de tempos em tempos repetindo o preparo.

### 4. Asaas (pagamentos)

Faça tudo primeiro no **sandbox** (`https://api-sandbox.asaas.com/v3`, que é o padrão do projeto) e só depois repita em produção (`https://api.asaas.com/v3`).

1. Crie a conta da plataforma e gere a chave de API no painel. Guarde-a no Secret Manager como `Payments__Asaas__ApiKey` (uma chave para o sandbox e outra para produção).
2. Cadastre uma **chave Pix** na conta (sem ela o QR code só vale até o fim do dia).
3. Gere um token aleatório de 32 a 255 caracteres sem espaços (`openssl rand -hex 32`) e guarde como `Payments__Asaas__WebhookToken`.
4. No painel, cadastre o webhook em `https://URL_DA_API/api/v1/webhooks/asaas`, com esse mesmo token no campo de autenticação e os eventos de cobrança (`PAYMENT_*`) e de assinatura (`SUBSCRIPTION_*`). Token errado ou ausente é recusado com 401.
5. Para o cartão salvo funcionar em produção, peça ao gerente de contas do Asaas para habilitar a tokenização de cartão (no sandbox já funciona).
6. Para o split, cada motorista vincula a própria conta Asaas no app (`PUT /api/v1/driver/payout-account` com o `walletId`). Sem isso, o Pix e o cartão do frete respondem `409 payments.driver_without_payout_account`.

Ligue: `--update-env-vars Payments__Provider=Asaas,Payments__Asaas__BaseUrl=https://api.asaas.com/v3 --update-secrets Payments__Asaas__ApiKey=asaas-api-key:latest,Payments__Asaas__WebhookToken=asaas-webhook-token:latest`.

Confira a tarifa do Pix na sua conta: ela sai da parte da plataforma, e se passar da taxa fixa de R$ 4,90 o Asaas recusa o split. A cobrança mínima do Asaas (R$ 5,00, confirme na sua conta) vale para o Pix da taxa do motorista. Teste de ponta a ponta no sandbox: assinar um plano, pagar o Pix de teste e ver o webhook chegar (o pagamento muda para `Paid`).

### 5. Mercado Pago (Apple Pay e carteiras)

1. Crie uma aplicação em "Suas integrações" no painel do Mercado Pago e peça à equipe comercial a habilitação do Apple Pay (só iOS). Confirme com eles se a sua conta tem cada carteira liberada antes de oferecê-la a clientes.
2. Na aplicação, cadastre o endereço de redirecionamento do OAuth: `https://URL_DA_API/api/v1/callbacks/mercadopago/oauth` (o mesmo valor de `Payments__MercadoPago__RedirectUri`).
3. Cadastre o webhook em `https://URL_DA_API/api/v1/webhooks/mercadopago` com o evento "Pagamentos" e copie a chave secreta dele.
4. Gere a chave que criptografa os tokens dos motoristas e guarde-a no Secret Manager: `openssl rand -base64 32`. **Perdê-la desconecta todos os motoristas.**
5. Ligue (os cinco primeiros valores são obrigatórios; sem eles a API não inicia com `MercadoPago`):

```bash
gcloud run services update SERVICE --project PROJECT --region REGION \
  --update-env-vars Payments__WalletProvider=MercadoPago,Payments__MercadoPago__ClientId=ID,Payments__MercadoPago__RedirectUri=https://URL_DA_API/api/v1/callbacks/mercadopago/oauth \
  --update-secrets Payments__MercadoPago__ClientSecret=mp-client-secret:latest,Payments__MercadoPago__WebhookSecret=mp-webhook-secret:latest,Payments__MercadoPago__TokenEncryptionKey=mp-token-key:latest
```

Opcionais: `Payments__MercadoPago__AppReturnUrl` (link do app para onde o navegador volta depois de o motorista conectar a conta) e `Payments__MercadoPago__NotificationUrl` (endereço do webhook enviado em cada pagamento, se você não o cadastrou no painel).

### 6. Primeiro admin e termos de uso

Em Staging e Production o admin e os termos **não são criados sozinhos** (isso só acontece em Development). Faça uma vez, depois das migrations:

1. Crie o admin: registre um usuário pelo app ou por `POST /api/v1/auth/register` (como cliente) e promova-o no banco:

```sql
UPDATE "Users" SET "Role" = 'Admin' WHERE "Email" = 'admin@seudominio.com.br';
```

2. Entre com esse usuário e publique os dois termos (`type` 1 = termos de uso, 2 = política de privacidade). O app exige o aceite deles antes de usar:

```bash
curl -X POST $URL/api/v1/admin/terms -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{ "type": 1, "version": "1.0", "title": "Termos de uso", "contentUrl": "https://seudominio.com.br/termos" }'
```

Repita com `"type": 2` para a política de privacidade. Publicar uma nova versão desativa a anterior do mesmo tipo.

3. O e-mail de aprovação e rejeição do cadastro ainda **não tem provedor real**: hoje ele só é gravado no log da API (`LoggingEmailSender`). O motorista é avisado também por notificação no app.

## Testes

```powershell
dotnet test
```
