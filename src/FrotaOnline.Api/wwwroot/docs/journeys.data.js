/* Journeys of the Frota Online API, one object per user journey.
 *
 * journey: { id, title, summary, who, flows: [flow] }
 * flow:    { title, intro, steps: [step] }
 * step:    {
 *   id, title, desc, method, path,
 *   role: 'Customer' | 'Driver' | 'Admin' | 'Any' | null   (null = public endpoint; Any = the role chosen in the session bar)
 *   query: { name: defaultValue }               query string fields the user can edit
 *   body: object                                 JSON template. A string that is exactly "{{name}}" becomes the saved value
 *                                                (numbers stay numbers); "{{name}}" inside text is replaced as text.
 *   bind: { routeParam: 'savedVariable' }        when the route parameter and the saved variable have different names
 *   capture: { variable: 'path.in[0].response' } values kept after a successful call ("req.field" reads the request body)
 *   auth: true                                   the response is a login: keep the tokens for the user's role
 *   kind: 'upload'                               the step sends a file to the signed URL instead of calling the API
 *   danger: true                                 the step cannot be undone: the button asks for confirmation
 *   note                                         one extra line shown under the description
 * }
 * Built-in variables: {{rand}}, {{plate}}, {{cpf}}, {{cnh}} (random, kept until you clear the saved values).
 */
window.FROTA_JOURNEYS = [
  {
    id: 'conta',
    title: 'Conta e sessão',
    who: 'Todos',
    summary: 'Termos, cadastro, login, renovação do token e saída. Todo usuário começa aqui.',
    flows: [
      {
        title: 'Criar a conta',
        intro: 'Leia os termos, crie a conta e aceite o que estiver pendente. Guarde os dois tokens: o access token vai em cada chamada e o refresh token renova a sessão.',
        steps: [
          {
            id: 'terms-active', role: null, method: 'GET', path: '/terms/active',
            title: 'Ler os termos ativos', desc: 'Mostre os textos antes do cadastro. Não precisa de token.',
            capture: { termId: '[0].id' }
          },
          {
            id: 'register', role: null, method: 'POST', path: '/auth/register', auth: true,
            title: 'Criar a conta', desc: 'Troque role por Driver para criar um motorista. A resposta já traz os tokens e fica salva na barra de sessão.',
            body: { fullName: 'Maria Souza', email: 'maria+{{rand}}@example.com', phoneNumber: '+5511999999999', password: 'Str0ng#Pass', role: 'Customer' },
            capture: { lastEmail: 'req.email', lastPassword: 'req.password' },
            note: 'Senha de 8 a 128 caracteres com maiúscula, minúscula e dígito. Contas Admin não se cadastram sozinhas.'
          },
          {
            id: 'terms-pending', role: 'Any', method: 'GET', path: '/terms/pending',
            title: 'Ver o que falta aceitar', desc: 'Lista vazia significa que não sobrou nada. Uma versão nova publicada pelo admin volta a aparecer aqui.',
            capture: { termId: '[0].id' }
          },
          {
            id: 'terms-accept', role: 'Any', method: 'POST', path: '/terms/accept',
            title: 'Aceitar um termo', desc: 'Uma chamada por termo. O cliente só cria frete e o motorista só envia o perfil para análise com tudo aceito.',
            body: { termDocumentId: '{{termId}}' }
          },
          {
            id: 'terms-consents', role: 'Any', method: 'GET', path: '/terms/consents',
            title: 'Ver os aceites feitos', desc: 'Histórico de aceites com data e versão.'
          }
        ]
      },
      {
        title: 'Entrar e manter a sessão',
        intro: 'O access token dura 15 minutos. Antes de expirar, troque o refresh token por um par novo. O refresh token só funciona uma vez.',
        steps: [
          {
            id: 'login', role: null, method: 'POST', path: '/auth/login', auth: true,
            title: 'Entrar', desc: 'Para a conta de teste criada acima, e-mail e senha já vêm preenchidos.',
            body: { email: '{{lastEmail}}', password: '{{lastPassword}}' }
          },
          {
            id: 'me', role: 'Any', method: 'GET', path: '/auth/me',
            title: 'Quem sou eu', desc: 'Devolve id, e-mail e perfil lidos do token.'
          },
          {
            id: 'refresh', role: null, method: 'POST', path: '/auth/refresh', auth: true,
            title: 'Renovar a sessão', desc: 'Troca o refresh token por um par novo. Guarde o par novo e descarte o antigo.',
            body: { refreshToken: '{{refreshToken}}' }
          },
          {
            id: 'logout', role: 'Any', method: 'POST', path: '/auth/logout',
            title: 'Sair', desc: 'Revoga o refresh token. Apague também os tokens do aparelho.',
            body: { refreshToken: '{{refreshToken}}' }
          }
        ]
      },
      {
        title: 'Recuperar a senha',
        intro: 'Para quem esqueceu a senha. O app pede o e-mail, a pessoa recebe um link com o token e define a nova senha. Nenhuma das duas chamadas precisa de login.',
        steps: [
          {
            id: 'pw-forgot', role: null, method: 'POST', path: '/auth/password/forgot',
            title: 'Pedir a recuperação', desc: 'Responde 204 para qualquer e-mail válido, exista a conta ou não, para não revelar quem está cadastrado. Se a conta existe e está ativa, o e-mail com o token é enviado.',
            body: { email: '{{lastEmail}}' },
            note: 'Sem Email__Provider=MailerSend a API só grava o e-mail no log. Em desenvolvimento, copie o token de lá.'
          },
          {
            id: 'pw-reset', role: null, method: 'POST', path: '/auth/password/reset',
            title: 'Definir a nova senha', desc: 'O token vale 30 minutos e uma única vez. Ao trocar a senha, todas as sessões da conta são encerradas e é preciso entrar de novo.',
            body: { token: 'cole-o-token-recebido-por-e-mail', newPassword: 'N0va#Senha1' },
            capture: { lastPassword: 'req.newPassword' },
            note: 'Token inválido ou expirado volta 400 com o código auth.invalid_reset_token. A senha segue a regra do cadastro: 8 a 128 caracteres com maiúscula, minúscula e dígito.'
          }
        ]
      }
    ]
  },

  {
    id: 'motorista-cadastro',
    title: 'Cadastro do motorista',
    who: 'Motorista',
    summary: 'Perfil, veículo, documentos e envio para análise até a aprovação.',
    flows: [
      {
        title: 'Montar o perfil',
        intro: 'Antes de enviar para análise o motorista precisa de perfil, ao menos um veículo ativo, um documento e todos os termos aceitos. O checklist do perfil diz o que falta.',
        steps: [
          {
            id: 'd-profile-get', role: 'Driver', method: 'GET', path: '/driver/profile',
            title: 'Consultar o perfil', desc: '404 significa que o perfil ainda não existe. Depois de criado, traz status e checklist.'
          },
          {
            id: 'd-profile-put', role: 'Driver', method: 'PUT', path: '/driver/profile',
            title: 'Criar ou atualizar o perfil', desc: 'CPF ou CNPJ válido, CNH com 11 dígitos e categoria A, B, C, D, E, AB, AC, AD ou AE.',
            body: { taxId: '{{cpf}}', cnhNumber: '{{cnh}}', cnhCategory: 'B' },
            note: 'O perfil trava enquanto está em análise.'
          },
          {
            id: 'd-vehicle-add', role: 'Driver', method: 'POST', path: '/driver/vehicles',
            title: 'Cadastrar um veículo', desc: 'Tipos: Fiorino, Van, Pickup, Truck34, Truck e Trailer. A placa é única na plataforma.',
            body: { type: 'Fiorino', plate: '{{plate}}', model: 'Fiat Fiorino', year: 2020, capacityKg: 600 },
            capture: { vehicleId: 'id' }
          },
          {
            id: 'd-vehicle-list', role: 'Driver', method: 'GET', path: '/driver/vehicles',
            title: 'Listar veículos ativos', desc: 'Só os veículos ativos aparecem.',
            capture: { vehicleId: '[0].id' }
          },
          {
            id: 'd-vehicle-del', role: 'Driver', method: 'DELETE', path: '/driver/vehicles/{vehicleId}',
            title: 'Desativar um veículo', desc: 'O veículo some da lista e deixa de contar para novas propostas.', danger: true
          }
        ]
      },
      {
        title: 'Enviar documentos',
        intro: 'O arquivo não passa pela API. Peça uma URL assinada, envie o arquivo direto para ela e registre a chave no final.',
        steps: [
          {
            id: 'd-upload-url', role: 'Driver', method: 'POST', path: '/driver/documents/upload-url',
            title: 'Pedir a URL de envio', desc: 'Tipos aceitos: image/jpeg, image/png e application/pdf, até 10 MB. kind pode ser DriverDocument ou VehiclePhoto.',
            body: { kind: 'DriverDocument', contentType: 'image/jpeg' },
            capture: { storageKey: 'storageKey', uploadUrl: 'uploadUrl', uploadMethod: 'method', uploadHeaders: 'headers', fileContentType: 'req.contentType' },
            note: 'Sem armazenamento configurado (Storage__Provider) esta chamada responde erro.'
          },
          {
            id: 'd-upload-put', role: null, kind: 'upload', method: 'PUT', path: '(uploadUrl)',
            title: 'Enviar o arquivo', desc: 'Escolha um arquivo do computador. Ele vai direto para a URL assinada, com os cabeçalhos devolvidos no passo anterior.'
          },
          {
            id: 'd-doc-register', role: 'Driver', method: 'POST', path: '/driver/documents',
            title: 'Registrar o arquivo', desc: 'Só depois do envio. Confirma que o arquivo existe e guarda a chave.',
            body: { kind: 'DriverDocument', storageKey: '{{storageKey}}', contentType: '{{fileContentType}}', sizeBytes: '{{fileSize}}' }
          },
          {
            id: 'd-doc-list', role: 'Driver', method: 'GET', path: '/driver/documents',
            title: 'Listar documentos', desc: 'Arquivos já registrados.'
          }
        ]
      },
      {
        title: 'Enviar para análise',
        intro: 'Com o checklist completo o motorista envia o perfil. Depois disso consulte o perfil até o status sair de PendingReview.',
        steps: [
          {
            id: 'd-checklist', role: 'Driver', method: 'GET', path: '/driver/profile',
            title: 'Conferir o checklist', desc: 'checklist.isReady precisa ser true. Se faltar algo, volte ao fluxo correspondente.'
          },
          {
            id: 'd-submit', role: 'Driver', method: 'POST', path: '/driver/submit',
            title: 'Enviar para análise', desc: 'O status vira PendingReview e o perfil trava. O admin aprova ou rejeita na jornada "Análise de motoristas".'
          }
        ]
      }
    ]
  },

  {
    id: 'admin-analise',
    title: 'Análise de motoristas',
    who: 'Admin',
    summary: 'Fila de análise, aprovar ou rejeitar, taxas do motorista e publicação de novos termos.',
    flows: [
      {
        title: 'Aprovar ou rejeitar',
        intro: 'Entre com a conta Admin (chip "Admin" na barra de sessão; em Development use admin@frotaonline.local). A decisão chega ao motorista por e-mail e notificação.',
        steps: [
          {
            id: 'a-login', role: null, method: 'POST', path: '/auth/login', auth: true,
            title: 'Entrar como admin', desc: 'Conta criada pelo seed de Development.',
            body: { email: 'admin@frotaonline.local', password: 'Admin#12345' }
          },
          {
            id: 'a-queue', role: 'Admin', method: 'GET', path: '/admin/drivers',
            title: 'Ver a fila', desc: 'Status: Draft, PendingReview, Approved, Rejected ou Removed. O padrão é PendingReview.',
            query: { status: 'PendingReview' },
            capture: { driverId: '[0].driverId' }
          },
          {
            id: 'a-detail', role: 'Admin', method: 'GET', path: '/admin/drivers/{driverId}',
            title: 'Abrir o perfil completo', desc: 'Dados, veículos e arquivos enviados.',
            capture: { fileId: 'files[0].id' }
          },
          {
            id: 'a-file', role: 'Admin', method: 'GET', path: '/admin/drivers/{driverId}/files/{fileId}/url',
            title: 'Abrir um arquivo', desc: 'Devolve uma URL de download que vale por 10 minutos.'
          },
          {
            id: 'a-approve', role: 'Admin', method: 'POST', path: '/admin/drivers/{driverId}/approve',
            title: 'Aprovar', desc: 'O motorista passa a operar e recebe e-mail e notificação.'
          },
          {
            id: 'a-reject', role: 'Admin', method: 'POST', path: '/admin/drivers/{driverId}/reject',
            title: 'Rejeitar', desc: 'O motivo é obrigatório e aparece para o motorista corrigir o cadastro.',
            body: { reason: 'Foto da CNH ilegível. Envie outra.' }
          }
        ]
      },
      {
        title: 'Taxas do motorista',
        intro: 'A plataforma registra a taxa de cada entrega concluída. O admin baixa o valor quando o motorista paga por fora.',
        steps: [
          {
            id: 'a-fees', role: 'Admin', method: 'GET', path: '/admin/drivers/{driverId}/fees',
            title: 'Ver o extrato', desc: 'Saldo, total lançado, total baixado e os 200 lançamentos mais recentes.'
          },
          {
            id: 'a-settle', role: 'Admin', method: 'POST', path: '/admin/drivers/{driverId}/fees/settle',
            title: 'Baixar um pagamento', desc: 'Registra que o motorista pagou parte ou toda a taxa.',
            body: { amount: 4.9, description: 'Pix recebido fora da plataforma' }
          }
        ]
      },
      {
        title: 'Novos termos',
        intro: 'Ao publicar uma versão nova, todos os usuários precisam aceitar de novo.',
        steps: [
          {
            id: 'a-term', role: 'Admin', method: 'POST', path: '/admin/terms',
            title: 'Publicar uma versão', desc: 'type: TermsOfUse ou PrivacyPolicy.',
            body: { type: 'TermsOfUse', version: '2.0', title: 'Termos de uso', contentUrl: 'https://example.com/termos-v2' }
          }
        ]
      }
    ]
  },

  {
    id: 'cliente-frete',
    title: 'Cliente pede um frete',
    who: 'Cliente',
    summary: 'Endereços, rota no mapa, criação do frete, ranking de propostas e aprovação.',
    flows: [
      {
        title: 'Endereços',
        intro: 'Cada coleta e cada entrega usa um endereço salvo. Envie latitude e longitude juntas para o mapa funcionar.',
        steps: [
          {
            id: 'c-addr-pickup', role: 'Customer', method: 'POST', path: '/addresses',
            title: 'Salvar o endereço de coleta', desc: 'Estado com sigla e CEP com 8 dígitos. Latitude e longitude são opcionais, mas vão juntas.',
            body: { label: 'Depósito', street: 'Av. Paulista', number: '1000', complement: null, district: 'Bela Vista', city: 'São Paulo', state: 'SP', zipCode: '01310-100', latitude: -23.5614, longitude: -46.6558 },
            capture: { pickupAddressId: 'id', addressId: 'id' }
          },
          {
            id: 'c-addr-delivery', role: 'Customer', method: 'POST', path: '/addresses',
            title: 'Salvar o endereço de entrega', desc: 'Mesmo formato. Este é o destino final do frete.',
            body: { label: 'Cliente final', street: 'Rua Barão de Jaguara', number: '500', complement: 'Sala 2', district: 'Centro', city: 'Campinas', state: 'SP', zipCode: '13015-001', latitude: -22.9056, longitude: -47.0608 },
            capture: { deliveryAddressId: 'id', addressId: 'id' }
          },
          {
            id: 'c-addr-list', role: 'Customer', method: 'GET', path: '/addresses',
            title: 'Listar endereços', desc: 'Só os endereços do próprio cliente, sem os apagados.'
          },
          {
            id: 'c-addr-edit', role: 'Customer', method: 'PUT', path: '/addresses/{addressId}',
            title: 'Editar um endereço', desc: 'Mesmo corpo da criação. Endereço de frete aberto ou em andamento não pode mudar (409).',
            body: { label: 'Depósito (novo)', street: 'Av. Paulista', number: '1000', complement: null, district: 'Bela Vista', city: 'São Paulo', state: 'SP', zipCode: '01310-100', latitude: -23.5614, longitude: -46.6558 }
          },
          {
            id: 'c-addr-del', role: 'Customer', method: 'DELETE', path: '/addresses/{addressId}',
            title: 'Apagar um endereço', desc: 'Se já foi usado em um frete, fica só no histórico.', danger: true
          }
        ]
      },
      {
        title: 'Criar o frete',
        intro: 'As paradas seguem a ordem do trajeto: a primeira é coleta e a última é entrega. Um frete pode ter várias coletas e entregas, com itens opcionais por parada.',
        steps: [
          {
            id: 'c-preview', role: 'Customer', method: 'POST', path: '/freights/route-preview',
            title: 'Ver a rota antes de criar', desc: 'Devolve distância em metros, tempo em segundos e a linha para desenhar no mapa. Se o motor de rotas falhar, distância e tempo vêm null.',
            body: { addressIds: ['{{pickupAddressId}}', '{{deliveryAddressId}}'] }
          },
          {
            id: 'c-create', role: 'Customer', method: 'POST', path: '/freights',
            title: 'Criar o frete', desc: 'Exige todos os termos aceitos. Os motoristas compatíveis recebem aviso.',
            body: {
              stops: [
                { type: 'Pickup', addressId: '{{pickupAddressId}}', note: 'Falar com a portaria', items: [{ description: 'Caixa de peças', quantity: 4 }] },
                { type: 'Delivery', addressId: '{{deliveryAddressId}}', note: null, items: [] }
              ],
              cargoDescription: 'Peças de reposição', cargoWeightKg: 120, requiredVehicleType: 'Fiorino', scheduledPickupAt: null
            },
            capture: { freightId: 'id' }
          },
          {
            id: 'c-list', role: 'Customer', method: 'GET', path: '/freights',
            title: 'Listar meus fretes', desc: 'Filtre por status: Open, Approved, Expired ou Cancelled.',
            query: { status: '' },
            capture: { freightId: '[0].id' }
          },
          {
            id: 'c-get', role: 'Customer', method: 'GET', path: '/freights/{freightId}',
            title: 'Abrir um frete', desc: 'Paradas, endereços completos, itens e quantas propostas aguardam resposta.'
          },
          {
            id: 'c-route', role: 'Customer', method: 'GET', path: '/freights/{freightId}/route',
            title: 'Rota de um frete criado', desc: 'Mesmo formato do preview.'
          }
        ]
      },
      {
        title: 'Escolher a proposta',
        intro: 'Os motoristas definem o preço. O ranking põe o menor preço primeiro. Aprovar cria a entrega e recusa as outras propostas.',
        steps: [
          {
            id: 'c-proposals', role: 'Customer', method: 'GET', path: '/freights/{freightId}/proposals',
            title: 'Ver o ranking', desc: 'Só propostas pendentes e dentro do prazo. Mostra nome do motorista e veículo, nunca dados pessoais.',
            capture: { proposalId: '[0].proposalId' }
          },
          {
            id: 'c-approve', role: 'Customer', method: 'POST', path: '/freights/{freightId}/proposals/{proposalId}/approve',
            title: 'Aprovar uma proposta', desc: 'Responde 201 com a entrega criada. Se o motorista já não puder pegar o frete, escolha outra proposta (409).',
            capture: { deliveryId: 'deliveryId' }
          },
          {
            id: 'c-cancel', role: 'Customer', method: 'POST', path: '/freights/{freightId}/cancel',
            title: 'Cancelar o frete', desc: 'Só enquanto está aberto. As propostas pendentes são rejeitadas.', danger: true
          }
        ]
      }
    ]
  },

  {
    id: 'motorista-propostas',
    title: 'Motorista encontra e propõe',
    who: 'Motorista aprovado',
    summary: 'Fretes abertos compatíveis, proposta com preço, acompanhamento e retirada.',
    flows: [
      {
        title: 'Achar um frete e propor',
        intro: 'A lista mostra só fretes que o veículo do motorista comporta (tipo e capacidade) e em que ele ainda não propôs. Antes de ganhar, o motorista vê o bairro e um ponto aproximado de cerca de 1 km, nunca a rua.',
        steps: [
          {
            id: 'p-open', role: 'Driver', method: 'GET', path: '/driver/freights/open',
            title: 'Listar fretes abertos', desc: 'Até 100, do mais novo para o mais antigo, com distância e tempo da rota.',
            capture: { freightId: '[0].id' }
          },
          {
            id: 'p-send', role: 'Driver', method: 'POST', path: '/driver/freights/{freightId}/proposals',
            title: 'Enviar uma proposta', desc: 'O veículo precisa ser do tipo pedido, ter capacidade e estar livre na agenda. Preço com até 2 casas decimais.',
            body: { vehicleId: '{{vehicleId}}', price: 450, message: 'Chego em 30 minutos' },
            capture: { driverProposalId: 'id' }
          },
          {
            id: 'p-list', role: 'Driver', method: 'GET', path: '/driver/proposals',
            title: 'Ver minhas propostas', desc: 'Status: Pending, Accepted, Rejected, Withdrawn ou Expired.',
            capture: { driverProposalId: '[0].id' }
          },
          {
            id: 'p-withdraw', role: 'Driver', method: 'POST', path: '/driver/proposals/{proposalId}/withdraw',
            title: 'Retirar uma proposta', desc: 'Só enquanto está Pending.',
            bind: { proposalId: 'driverProposalId' }, danger: true
          }
        ]
      }
    ]
  },

  {
    id: 'entrega',
    title: 'Andamento da entrega',
    who: 'Motorista e cliente',
    summary: 'Do início ao último ponto: rota, paradas concluídas, ocorrências e cancelamento.',
    flows: [
      {
        title: 'Motorista executa a entrega',
        intro: 'As paradas seguem a ordem. A primeira coleta coloca a entrega em PickedUp, as seguintes em InTransit e a última parada conclui (Delivered).',
        steps: [
          {
            id: 'e-list', role: 'Driver', method: 'GET', path: '/driver/deliveries',
            title: 'Minhas entregas', desc: 'Entregas ganhas, com contadores de paradas.',
            capture: { deliveryId: '[0].deliveryId', freightId: '[0].freightRequestId' }
          },
          {
            id: 'e-route-full', role: 'Driver', method: 'GET', path: '/driver/deliveries/{deliveryId}/route',
            title: 'Rota completa', desc: 'Todas as paradas com endereço completo e itens.',
            query: { view: 'Full' }, capture: { stopId: 'stops[0].stopId' }
          },
          {
            id: 'e-route-next', role: 'Driver', method: 'GET', path: '/driver/deliveries/{deliveryId}/route',
            title: 'Só a próxima parada', desc: 'Útil para a tela de navegação. Guarda o id da parada a concluir.',
            query: { view: 'Next' }, capture: { stopId: 'stops[0].stopId' }
          },
          {
            id: 'e-map', role: 'Driver', method: 'GET', path: '/driver/deliveries/{deliveryId}/map',
            title: 'Rota no mapa', desc: 'Pontos, distância, tempo e a linha a desenhar.'
          },
          {
            id: 'e-start', role: 'Driver', method: 'POST', path: '/driver/deliveries/{deliveryId}/start',
            title: 'Iniciar a entrega', desc: 'Scheduled vira ToPickup e o cliente é avisado.'
          },
          {
            id: 'e-location', role: 'Driver', method: 'PUT', path: '/driver/deliveries/{deliveryId}/location',
            title: 'Enviar a posição', desc: 'Envie a cada 10 a 15 segundos enquanto a entrega está em ToPickup, PickedUp ou InTransit. A API guarda só a última posição, e o cliente a lê no status da entrega.',
            body: { latitude: -22.9056, longitude: -47.0608, heading: 90, speedKmh: 40 },
            note: 'heading (0 a 360) e speedKmh são opcionais. Fora de uma entrega em andamento a resposta é 409.'
          },
          {
            id: 'e-sign-register', role: 'Driver', method: 'POST', path: '/signatures',
            title: 'Assinatura: enviar', desc: 'Obrigatória antes de concluir a última parada: a entrega só vira Delivered com a assinatura do cliente e a do motorista. Envie a imagem em base64 (PNG ou JPEG, até 2 MB). A API a comprime e guarda no banco, com o frete, o cliente e o motorista da entrega. Uma por tipo e por frete.',
            body: { type: 'Driver', freightId: '{{freightId}}', image: 'data:image/png;base64,...' }
          },
          {
            id: 'e-complete', role: 'Driver', method: 'POST', path: '/driver/deliveries/{deliveryId}/stops/{stopId}/complete',
            title: 'Concluir a parada', desc: 'Só a próxima da ordem. Tudo é opcional; latitude e longitude vão juntas. A resposta traz a próxima parada. Repita a rota "Next" para pegar o novo id. A última parada só conclui com as assinaturas do cliente e do motorista (409 delivery.signatures_missing).',
            body: { note: 'Entregue na portaria', latitude: -22.9056, longitude: -47.0608, proofMediaFileId: null },
            capture: { stopId: 'nextStopId' }
          },
          {
            id: 'e-incident', role: 'Driver', method: 'POST', path: '/driver/deliveries/{deliveryId}/incident',
            title: 'Registrar uma ocorrência', desc: 'Avisa o cliente de um problema no caminho.',
            body: { note: 'Pneu furado, atraso de 40 minutos' }
          },
          {
            id: 'e-cancel-d', role: 'Driver', method: 'POST', path: '/driver/deliveries/{deliveryId}/cancel',
            title: 'Cancelar como motorista', desc: 'O cliente é avisado e o frete grátis (sem assinatura) volta ao motorista.',
            body: { reason: 'Veículo quebrou' }, danger: true
          }
        ]
      },
      {
        title: 'Cliente acompanha',
        intro: 'O cliente vê motorista, veículo, andamento e a linha do tempo, e pode cancelar até a coleta.',
        steps: [
          {
            id: 'e-c-list', role: 'Customer', method: 'GET', path: '/deliveries',
            title: 'Minhas entregas', desc: 'Com motorista, veículo, contador de paradas e situação do pagamento.',
            capture: { deliveryId: '[0].deliveryId', freightId: '[0].freightRequestId' }
          },
          {
            id: 'e-c-get', role: 'Customer', method: 'GET', path: '/deliveries/{deliveryId}',
            title: 'Detalhe e linha do tempo', desc: 'Paradas, eventos com horário e pagamento.'
          },
          {
            id: 'e-c-sign-register', role: 'Customer', method: 'POST', path: '/signatures',
            title: 'Assinatura: enviar', desc: 'Obrigatória antes de concluir a última parada: a entrega só vira Delivered com a assinatura do cliente e a do motorista. Envie a imagem em base64 (PNG ou JPEG, até 2 MB). A API a comprime e guarda no banco, com o frete, o cliente e o motorista da entrega. Uma por tipo e por frete.',
            body: { type: 'Customer', freightId: '{{freightId}}', image: 'data:image/png;base64,...' }
          },
          {
            id: 'e-c-status', role: 'Customer', method: 'GET', path: '/deliveries/{deliveryId}/status',
            title: 'Acompanhar ao vivo (leve)', desc: 'Para o polling: status, paradas concluídas, pagamento, customerSigned e driverSigned (assinaturas já registradas), a posição do motorista (driverLocation, só com a entrega em andamento) e version. Busque o detalhe completo só quando o version mudar.'
          },
          {
            id: 'e-c-cancel', role: 'Customer', method: 'POST', path: '/deliveries/{deliveryId}/cancel',
            title: 'Cancelar como cliente', desc: 'Só antes da coleta (Scheduled ou ToPickup). O motorista é avisado.',
            body: { reason: 'Não preciso mais do frete' }, danger: true
          }
        ]
      }
    ]
  },

  {
    id: 'pagamentos',
    title: 'Pagamentos',
    who: 'Cliente e motorista',
    summary: 'Pix, cartão e carteira digital do cliente. Recebimento, assinatura e taxa do motorista.',
    flows: [
      {
        title: 'Cliente paga a entrega',
        intro: 'O dinheiro vai para a conta do motorista; a plataforma fica só com a taxa. O pagamento fica Pending até o provedor confirmar por webhook.',
        steps: [
          {
            id: 'pg-pix', role: 'Customer', method: 'POST', path: '/deliveries/{deliveryId}/pix',
            title: 'Pagar com Pix', desc: 'Devolve o código copia e cola e o QR Code. O motorista precisa ter informado a conta de recebimento (walletId).',
            body: { taxId: '52998224725' }
          },
          {
            id: 'pg-card', role: 'Customer', method: 'POST', path: '/deliveries/{deliveryId}/card',
            title: 'Pagar com cartão', desc: 'Cartão novo (card + holder) ou salvo (savedCardId), nunca os dois. O número não é guardado; com saveCard só o token fica.',
            body: {
              savedCardId: null,
              card: { holderName: 'MARIA SOUZA', number: '4111 1111 1111 1111', expiryMonth: '12', expiryYear: '2030', ccv: '123' },
              holder: { taxId: '52998224725', postalCode: '01310-100', addressNumber: '1000', phone: null },
              saveCard: false
            },
            note: 'Use apenas cartões de teste do sandbox do provedor.'
          },
          {
            id: 'pg-wallet', role: 'Customer', method: 'POST', path: '/deliveries/{deliveryId}/wallet',
            title: 'Pagar com Apple Pay', desc: 'O app obtém o token pelo SDK do Mercado Pago e envia aqui. O motorista precisa ter conectado a conta Mercado Pago.',
            body: { cardToken: 'TOKEN_DO_SDK', paymentMethodId: 'visa' }
          },
          {
            id: 'pg-cards', role: 'Customer', method: 'GET', path: '/customer/cards',
            title: 'Cartões salvos', desc: 'Bandeira e 4 últimos dígitos, até 5 por pessoa.',
            capture: { cardId: '[0].id' }
          },
          {
            id: 'pg-card-del', role: 'Customer', method: 'DELETE', path: '/customer/cards/{cardId}',
            title: 'Remover um cartão salvo', desc: 'Apaga o token guardado.', danger: true
          }
        ]
      },
      {
        title: 'Motorista recebe',
        intro: 'Para receber Pix com split o motorista informa o walletId da própria conta Asaas. Para carteira digital, conecta a conta Mercado Pago uma vez. Pagamento por fora é confirmado à mão.',
        steps: [
          {
            id: 'pg-payout-get', role: 'Driver', method: 'GET', path: '/driver/payout-account',
            title: 'Ver a conta de recebimento', desc: 'Informa se já está configurada.'
          },
          {
            id: 'pg-payout-put', role: 'Driver', method: 'PUT', path: '/driver/payout-account',
            title: 'Informar o walletId do Asaas', desc: 'Um UUID da conta Asaas do motorista.',
            body: { walletId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' }
          },
          {
            id: 'pg-wallet-get', role: 'Driver', method: 'GET', path: '/driver/wallet-account',
            title: 'Conta Mercado Pago conectada?', desc: 'connected: true quando já autorizou.'
          },
          {
            id: 'pg-wallet-auth', role: 'Driver', method: 'POST', path: '/driver/wallet-account/authorization',
            title: 'Iniciar a conexão', desc: 'Devolve a URL de autorização do Mercado Pago. Abra no navegador do motorista.'
          },
          {
            id: 'pg-wallet-del', role: 'Driver', method: 'DELETE', path: '/driver/wallet-account',
            title: 'Desconectar', desc: 'Apaga os tokens guardados.', danger: true
          },
          {
            id: 'pg-confirm', role: 'Driver', method: 'POST', path: '/driver/deliveries/{deliveryId}/payment',
            title: 'Confirmar pagamento recebido por fora', desc: 'Dinheiro, Pix direto ou outro meio. method: Pix, Cash, Card ou Other.',
            body: { method: 'Cash', reference: 'Recibo 123' }
          }
        ]
      },
      {
        title: 'Assinatura do motorista',
        intro: 'O plano básico vale para 1 a 3 veículos e o completo não tem limite. O motorista paga numa página do Asaas, nenhum dado de cartão passa pela API.',
        steps: [
          {
            id: 'pg-sub-get', role: 'Driver', method: 'GET', path: '/driver/subscription',
            title: 'Ver planos e assinatura', desc: 'Planos com preço e a assinatura atual, se houver.'
          },
          {
            id: 'pg-sub-post', role: 'Driver', method: 'POST', path: '/driver/subscription',
            title: 'Assinar um plano', desc: 'Devolve paymentUrl. Abra a página para o motorista pagar. plan: Basic ou Full.',
            body: { plan: 'Basic' }
          },
          {
            id: 'pg-sub-del', role: 'Driver', method: 'DELETE', path: '/driver/subscription',
            title: 'Cancelar a assinatura', desc: 'O acesso vale até o fim do período pago.', danger: true
          }
        ]
      },
      {
        title: 'Taxa da plataforma',
        intro: 'Cada entrega concluída lança uma taxa. O motorista paga por Pix, ou o admin baixa o valor.',
        steps: [
          {
            id: 'pg-fees', role: 'Driver', method: 'GET', path: '/driver/fees',
            title: 'Extrato de taxas', desc: 'Saldo devedor e os 200 lançamentos mais recentes.'
          },
          {
            id: 'pg-fees-pix', role: 'Driver', method: 'POST', path: '/driver/fees/pix',
            title: 'Gerar Pix para quitar', desc: 'Devolve o Pix do saldo pendente. A baixa vem pelo webhook.'
          }
        ]
      }
    ]
  },

  {
    id: 'notificacoes',
    title: 'Notificações',
    who: 'Todos',
    summary: 'Registrar o aparelho, caixa de entrada, marcar como lida e preferências.',
    flows: [
      {
        title: 'Receber avisos',
        intro: 'O push chega pelo Firebase. A caixa de entrada guarda tudo, mesmo com o push desligado.',
        steps: [
          {
            id: 'n-device', role: 'Any', method: 'PUT', path: '/devices',
            title: 'Registrar o aparelho', desc: 'Chame ao abrir o app e quando o token do Firebase mudar. platform: Android, Ios ou Web.',
            body: { token: 'fcm-token-{{rand}}', platform: 'Android' },
            capture: { deviceToken: 'req.token' }
          },
          {
            id: 'n-device-del', role: 'Any', method: 'DELETE', path: '/devices',
            title: 'Remover o aparelho', desc: 'Ao sair da conta. Sem token, remove todos os aparelhos do usuário.',
            query: { token: '{{deviceToken}}' }, danger: true
          }
        ]
      },
      {
        title: 'Caixa de entrada',
        intro: 'Cada item traz targetType e targetId para abrir a tela certa. hasMore indica se há outra página.',
        steps: [
          {
            id: 'n-list', role: 'Any', method: 'GET', path: '/notifications',
            title: 'Listar', desc: 'page começa em 1 e pageSize vai de 1 a 100.',
            query: { unreadOnly: 'false', page: '1', pageSize: '20' },
            capture: { notificationId: 'items[0].id' }
          },
          {
            id: 'n-count', role: 'Any', method: 'GET', path: '/notifications/unread-count',
            title: 'Contador do selo', desc: 'Só o número de não lidas.'
          },
          {
            id: 'n-read', role: 'Any', method: 'POST', path: '/notifications/{notificationId}/read',
            title: 'Marcar uma como lida', desc: 'Chame ao abrir o item.'
          },
          {
            id: 'n-read-all', role: 'Any', method: 'POST', path: '/notifications/read-all',
            title: 'Marcar todas como lidas', desc: 'Devolve quantas mudaram.'
          }
        ]
      },
      {
        title: 'Preferências',
        intro: 'Os dois botões nascem ligados. Envie só o que mudou.',
        steps: [
          {
            id: 'n-pref-get', role: 'Any', method: 'GET', path: '/notifications/preferences',
            title: 'Ver preferências', desc: 'newFreightAlerts (avisos de frete compatível) e pushEnabled (todos os pushes).'
          },
          {
            id: 'n-pref-put', role: 'Any', method: 'PUT', path: '/notifications/preferences',
            title: 'Alterar', desc: 'Campo omitido mantém o valor atual.',
            body: { newFreightAlerts: false }
          }
        ]
      }
    ]
  },

  {
    id: 'privacidade',
    title: 'Privacidade (LGPD)',
    who: 'Todos',
    summary: 'Exportar os dados da pessoa e apagar a conta.',
    flows: [
      {
        title: 'Direitos do titular',
        intro: 'Qualquer usuário logado pode pedir uma cópia dos próprios dados e a exclusão da conta.',
        steps: [
          {
            id: 'l-export', role: 'Any', method: 'GET', path: '/me/data',
            title: 'Exportar meus dados', desc: 'Tudo o que a plataforma guarda sobre a pessoa, num único JSON.'
          },
          {
            id: 'l-delete', role: 'Any', method: 'POST', path: '/me/deletion',
            title: 'Apagar minha conta', desc: 'Pede a senha e confirm: true, para uma chamada perdida não apagar nada. Alguns dados fiscais são retidos por lei e a resposta lista quais.',
            body: { password: 'Str0ng#Pass', confirm: false }, danger: true
          }
        ]
      }
    ]
  }
];
