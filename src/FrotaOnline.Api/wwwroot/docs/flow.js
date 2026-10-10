/* Call-order diagram drawn on a canvas.
 * Tabs switch the flow, Play walks the happy path, a click opens the details of a step.
 * Layout is computed from the canvas width, so narrow screens fall back to a single column. */
(() => {
  'use strict';

  const app = document.getElementById('flow-app');
  const canvas = document.getElementById('flow-canvas');
  if (!app || !canvas || !canvas.getContext) return;

  const ctx = canvas.getContext('2d');
  const stage = canvas.parentElement;
  const PT = document.documentElement.lang.toLowerCase().startsWith('pt');
  const tr = (en, pt) => (PT ? pt : en);
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  const css = getComputedStyle(document.documentElement);
  const token = (name, fallback) => css.getPropertyValue(name).trim() || fallback;
  const C = {
    ink: '#0b0b0c',
    red: token('--red', '#d7141a'),
    redDeep: token('--red-deep', '#8f0d12'),
    panel: token('--panel', '#f3f3f4'),
    line: token('--line', '#e4e4e7'),
    soft: token('--ink-soft', '#62626b'),
    grey: '#9a9aa3',
    tint: '#fff3f3',
    white: '#ffffff'
  };
  const F = {
    mono: '"JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, Consolas, monospace',
    body: 'Manrope, system-ui, -apple-system, "Segoe UI", sans-serif',
    disp: 'Sora, "Segoe UI", system-ui, sans-serif'
  };

  /* ------------------------------------------------------------------ data */

  // Node fields: id, c (column), r (row, wide layout), m (method), p (path) or t (title),
  // d (one line under the title), k ('' | 'decision' | 'go' | 'soft'), who, send, get, note.
  // The order of the nodes array is the top-to-bottom order on narrow screens.
  const FLOWS = [
    {
      id: 'session',
      label: 'Session',
      intro: 'Every user starts here: read the terms, get a session, accept what is pending. Then open the tab that matches the account role.',
      nodes: [
        { id: 's1', c: 0, r: 0, m: 'GET', p: '/terms/active', d: 'Show the terms before sign-up', who: 'Anyone',
          get: '[{ id, type, version, title,\n  contentUrl, publishedAt }]',
          note: 'No token needed. Let the user read the texts before creating an account.' },
        { id: 's2', c: 0, r: 1, m: 'POST', p: '/auth/register', d: 'or /auth/login. Keep both tokens', who: 'Anyone',
          send: '{ "fullName": "Maria Souza",\n  "email": "maria@example.com",\n  "phoneNumber": "+5511999999999",\n  "password": "Str0ng#Pass",\n  "role": "Customer" }   // or "Driver"\n\nlogin: { "email", "password" }',
          get: '{ accessToken, accessTokenExpiresAt,\n  refreshToken, refreshTokenExpiresAt,\n  user: { id, fullName, email, role } }',
          note: 'Send the access token as Authorization: Bearer. It lasts 15 minutes; see the token tab for renewing it. user.role tells you which flow to show.' },
        { id: 's3', c: 0, r: 2, m: 'GET', p: '/terms/pending', d: 'What the user still has to accept', who: 'Signed in',
          get: '[{ id, type, version, title, contentUrl }]',
          note: 'An empty list means nothing is left. A new term version published by an admin shows up here again.' },
        { id: 's4', c: 0, r: 3, k: 'decision', t: 'Any term left?', d: 'Is the list not empty?', who: 'Front-end',
          note: 'Customers need every active term accepted to create a freight. Drivers need it before sending the profile to review.' },
        { id: 's5', c: 1, r: 3, m: 'POST', p: '/terms/accept', d: 'One call per term', who: 'Signed in',
          send: '{ "termDocumentId": "3fa85f64-..." }',
          get: '{ termDocumentId, type, version, acceptedAt }',
          note: 'The time and IP address are stored. Accepting the same term twice returns the first acceptance.' },
        { id: 's6', c: 0, r: 4, k: 'go', t: 'Open the role flow', d: 'Driver, Customer or Admin tab', who: 'Front-end',
          note: 'Route the user by user.role from the login or register response.' }
      ],
      edges: [
        { a: 's1', b: 's2' }, { a: 's2', b: 's3' }, { a: 's3', b: 's4' },
        { a: 's4', b: 's5', label: 'yes' }, { a: 's5', b: 's3', label: 'repeat' },
        { a: 's4', b: 's6', label: 'no' }
      ],
      seq: ['s1', 's2', 's3', 's4', 's5', 's3', 's4', 's6']
    },

    {
      id: 'driver',
      label: 'Driver',
      cols: ['Onboarding', 'Bidding and run'],
      intro: 'A driver cannot see freights until an admin approves the profile. Once approved, the right column opens.',
      nodes: [
        { id: 'd1', c: 0, r: 0, m: 'GET', p: '/driver/profile', d: '404 means there is no profile yet', who: 'Driver',
          get: '{ id, taxId, cnhNumber, cnhCategory,\n  status, rejectionReason,\n  checklist: { termsAccepted,\n    hasActiveVehicle, hasDocument,\n    isReady } }',
          note: 'status is Draft, PendingReview, Approved or Rejected. A 404 (driver.profile_not_found) means the next call is the PUT.' },
        { id: 'd2', c: 0, r: 1, m: 'PUT', p: '/driver/profile', d: 'taxId (CPF or CNPJ), CNH and category', who: 'Driver',
          send: '{ "taxId": "52998224725",\n  "cnhNumber": "12345678900",\n  "cnhCategory": "B" }',
          get: '200 with the profile (same shape as GET)',
          note: 'taxId is required and may be a CPF or a CNPJ (letters allowed). It is checked by its digits before saving. Editable only while Draft or Rejected.' },
        { id: 'd3', c: 0, r: 2, m: 'POST', p: '/driver/vehicles', d: 'At least one active vehicle', who: 'Driver',
          send: '{ "type": "Fiorino",\n  "plate": "ABC1D23",\n  "model": "Fiat Fiorino",\n  "year": 2020,\n  "capacityKg": 600 }',
          get: '201 { id, type, plate, model, year,\n  capacityKg, isActive }',
          note: 'type is Fiorino, Van, Pickup, Truck34, Truck or Trailer. Plates accept ABC1234 and ABC1D23.' },
        { id: 'd4', c: 0, r: 3, t: 'Upload the file to storage', d: 'Key: drivers/{userId}/name.jpg', who: 'Driver',
          note: 'This happens outside the API. The key must start with drivers/{your user id}/. A presigned-upload endpoint is not built yet, so today the front-end only registers the key.' },
        { id: 'd5', c: 0, r: 4, m: 'POST', p: '/driver/documents', d: 'Register the uploaded file', who: 'Driver',
          send: '{ "kind": "DriverDocument",\n  "storageKey": "drivers/{userId}/cnh.jpg",\n  "contentType": "image/jpeg",\n  "sizeBytes": 245000 }',
          get: '201 { id, kind, storageKey,\n  contentType, sizeBytes, createdAt }' },
        { id: 'd6', c: 0, r: 5, k: 'decision', t: 'checklist.isReady?', d: 'Read it from GET /driver/profile', who: 'Front-end',
          note: 'Ready means: terms accepted, one active vehicle and one document. If not, fix the missing item and check again.' },
        { id: 'd7', c: 0, r: 6, m: 'POST', p: '/driver/submit', d: 'Status becomes PendingReview', who: 'Driver',
          get: '200 with the profile, status PendingReview',
          note: 'Fails with the list of missing items when the checklist is incomplete. The profile is locked until the admin decides.' },
        { id: 'd8', c: 0, r: 7, m: 'GET', p: '/driver/profile', d: 'Poll until it leaves PendingReview', who: 'Driver',
          note: 'There is no push yet. The admin decision also sends an e-mail to the driver.' },
        { id: 'd9', c: 0, r: 8, k: 'decision', t: 'Status?', d: 'Approved or Rejected', who: 'Front-end',
          note: 'Rejected comes with rejectionReason. Show it, let the driver edit and submit again.' },
        { id: 'd10', c: 0, r: 9, k: 'go', t: 'Approved: can bid', d: 'Opens the freight board', who: 'Driver' },

        { id: 'm1', c: 1, r: 0, m: 'GET', p: '/driver/freights/open', d: 'Open freights that fit your vehicles', who: 'Driver (approved)',
          get: '[{ id, cargoDescription, cargoWeightKg,\n  requiredVehicleType, scheduledPickupAt,\n  proposalDeadline, pickupCount, deliveryCount,\n  stops: [{ sequence, type, district, city,\n    state, items }] }]',
          note: 'Shows how many pickups and deliveries the run has, and for each stop its area and its items. Only freights inside their proposal window whose vehicle type matches one of your active vehicles (up to 100). Street addresses stay hidden until your proposal is approved.' },
        { id: 'm2', c: 1, r: 1, m: 'POST', p: '/driver/freights/{freightId}/proposals', d: 'You set the price', who: 'Driver (approved)',
          send: '{ "vehicleId": "3fa85f64-...",\n  "price": 150.00,\n  "message": "Can pick up today" }',
          get: '201 with the proposal',
          note: 'One pending proposal per freight. The vehicle must be active, of the required type and carry the weight. Same-day freights take proposals for 5 minutes, scheduled ones for 24 hours.' },
        { id: 'm3', c: 1, r: 2, m: 'GET', p: '/driver/proposals', d: 'Pending, Accepted, Rejected, Expired', who: 'Driver (approved)',
          get: '[{ id, freightRequestId, vehicleId,\n  price, message, status, expiresAt }]',
          note: 'Accepted means the customer picked you and a delivery now exists.' },
        { id: 'm4', c: 1, r: 3, k: 'soft', m: 'POST', p: '/driver/proposals/{proposalId}/withdraw', d: 'Take back a pending proposal', who: 'Driver (approved)',
          note: 'Only while the proposal is Pending.' },
        { id: 'm5', c: 1, r: 4, m: 'GET', p: '/driver/deliveries', d: 'Deliveries you won', who: 'Driver (approved)',
          get: '[{ deliveryId, freightRequestId, status,\n  agreedPrice, scheduledPickupAt,\n  totalStops, completedStops }]',
          note: 'A proposal that turns Accepted creates a delivery here.' },
        { id: 'm6', c: 1, r: 5, m: 'GET', p: '/driver/deliveries/{id}/route', d: 'Full run or one stop at a time', who: 'Driver (approved)',
          get: '{ deliveryId, view, totalStops, completedStops,\n  stops: [{ stopId, sequence, type,\n    address: { street, number, city, ... },\n    note, items, completedAt }] }',
          note: 'Now with full addresses. ?view=Full returns every pickup and delivery at once, as an overview of the run. ?view=Next returns only the first stop not completed yet, so the app can show one stop after the other. Each stop lists the items to collect or hand over.' }
      ],
      edges: [
        { a: 'd1', b: 'd2' }, { a: 'd2', b: 'd3' }, { a: 'd3', b: 'd4' }, { a: 'd4', b: 'd5' },
        { a: 'd5', b: 'd6' },
        { a: 'd6', b: 'd3', label: 'no' }, { a: 'd6', b: 'd7', label: 'yes' },
        { a: 'd7', b: 'd8' }, { a: 'd8', b: 'd9' },
        { a: 'd9', b: 'd2', label: 'Rejected' }, { a: 'd9', b: 'd10', label: 'Approved' },
        { a: 'd10', b: 'm1' }, { a: 'm1', b: 'm2' }, { a: 'm2', b: 'm3' },
        { a: 'm3', b: 'm4', dashed: true, label: 'optional' },
        { a: 'm3', b: 'm5', label: 'Accepted' }, { a: 'm5', b: 'm6' }
      ],
      seq: ['d1', 'd2', 'd3', 'd4', 'd5', 'd6', 'd7', 'd8', 'd9', 'd10', 'm1', 'm2', 'm3', 'm5', 'm6']
    },

    {
      id: 'customer',
      label: 'Customer',
      cols: ['Order a freight', 'On the side'],
      intro: 'The customer posts the freight, watches proposals arrive ranked by price and approves one. Approval creates the delivery.',
      nodes: [
        { id: 'c1', c: 0, r: 0, m: 'POST', p: '/addresses', d: 'Every pickup and delivery address, saved once', who: 'Customer',
          send: '{ "label": "Warehouse",\n  "street": "Rua A", "number": "10",\n  "district": "Centro", "city": "Sao Paulo",\n  "state": "SP", "zipCode": "01001000" }',
          get: '201 with the address and its id',
          note: 'Register one address per pickup and per delivery. State must be a valid UF and the ZIP has 8 digits. The ids go into the next call. GET /addresses lists the saved ones. Customers can be people or companies.' },
        { id: 'c2', c: 0, r: 1, m: 'POST', p: '/freights', d: 'Stops in order: pickups, then deliveries', who: 'Customer',
          send: '{ "stops": [\n    { "type": "Pickup", "addressId": "3fa8...", "note": "Gate 2" },\n    { "type": "Pickup", "addressId": "9b1d..." },\n    { "type": "Delivery", "addressId": "7c9e...",\n      "items": [{ "description": "Brake pads", "quantity": 12 }] },\n    { "type": "Delivery", "addressId": "5d2a...", "note": "Final" } ],\n  "cargoDescription": "Auto parts",\n  "cargoWeightKg": 300,\n  "requiredVehicleType": "Fiorino",\n  "scheduledPickupAt": null }',
          get: '201 { id, status: "Open", proposalDeadline,\n  pickupCount, deliveryCount,\n  stops: [{ sequence, type, address, note,\n    items, completedAt }], ... }',
          note: 'The order of the stops array is the order the driver follows. The first stop must be a Pickup and the last a Delivery: that is the final delivery address. Items are optional and can go on pickups or deliveries. Every address must be different and yours. Without scheduledPickupAt the freight is for today and the proposal window is 5 minutes; with a future UTC date (ending in Z) it is up to 24 hours. 403 freight.terms_not_accepted when a term is missing.' },
        { id: 'c3', c: 0, r: 2, m: 'GET', p: '/freights/{id}/proposals', d: 'Ranked by price, cheapest first', who: 'Customer',
          get: '[{ rank, proposalId, price, message,\n  driverName, vehicleType, vehicleModel,\n  expiresAt, createdAt }]',
          note: 'Ties go to the earliest proposal. Poll while the freight is Open.' },
        { id: 'c4', c: 0, r: 3, k: 'decision', t: 'Pick a driver?', d: 'Compare price and vehicle', who: 'Front-end' },
        { id: 'c5', c: 0, r: 4, k: 'go', m: 'POST', p: '/freights/{id}/proposals/{proposalId}/approve', d: '201: delivery created', who: 'Customer',
          get: '201 { deliveryId, freightRequestId,\n  proposalId, driverProfileId,\n  agreedPrice, platformFee,\n  status: "Scheduled",\n  agendaStartsAt, agendaEndsAt }',
          note: 'Creates the delivery, books the driver (2 hours plus 1 hour per stop, from the first pickup), rejects the other proposals and closes the freight. Approve before the proposal window ends. 409 proposal.driver_busy means the driver got booked in the meantime: pick another.' },
        { id: 'c6', c: 1, r: 3, k: 'soft', m: 'POST', p: '/freights/{id}/cancel', d: 'Give up on an open freight', who: 'Customer',
          get: '204',
          note: 'Only while the freight is Open. Pending proposals are rejected.' },
        { id: 'c7', c: 1, r: 1, k: 'soft', m: 'GET', p: '/freights', d: 'Status of your freights, any time', who: 'Customer',
          get: '[{ id, status, proposalDeadline,\n  pendingProposals, ... }]',
          note: 'Filter with ?status=Open, Approved, Expired or Cancelled. GET /freights/{id} returns a single one. An Open freight may show as Open for up to 30 seconds after its window ends, until the expiry job runs.' }
      ],
      edges: [
        { a: 'c1', b: 'c2' }, { a: 'c2', b: 'c3' }, { a: 'c3', b: 'c4' },
        { a: 'c4', b: 'c5', label: 'approve' }, { a: 'c4', b: 'c3', label: 'wait' },
        { a: 'c4', b: 'c6', label: 'cancel' },
        { a: 'c2', b: 'c7', dashed: true, label: 'any time' }
      ],
      seq: ['c1', 'c2', 'c3', 'c4', 'c5']
    },

    {
      id: 'admin',
      label: 'Admin',
      intro: 'The admin reviews drivers waiting in the queue. The driver receives an e-mail with the decision.',
      nodes: [
        { id: 'a1', c: 0, r: 0, m: 'POST', p: '/auth/login', d: 'Account with the Admin role', who: 'Admin',
          send: '{ "email": "admin@frotaonline.local",\n  "password": "..." }',
          note: 'In Development the API seeds this admin on the first start, using Seed:Admin from appsettings.Development.json.' },
        { id: 'a2', c: 0, r: 1, m: 'GET', p: '/admin/drivers', d: 'Review queue, oldest first', who: 'Admin',
          get: '[{ driverId, userId, fullName, email,\n  phoneNumber, status, lastUpdatedAt }]',
          note: 'Defaults to PendingReview. Filter with ?status=.' },
        { id: 'a3', c: 0, r: 2, m: 'GET', p: '/admin/drivers/{id}', d: 'Profile, vehicles and files', who: 'Admin',
          get: '{ driverId, fullName, email, phoneNumber,\n  taxId, cnhNumber, cnhCategory, status,\n  vehicles: [...], files: [...] }' },
        { id: 'a4', c: 0, r: 3, k: 'decision', t: 'Decision', d: 'Approve or reject', who: 'Admin' },
        { id: 'a5', c: 0, r: 4, k: 'go', m: 'POST', p: '/admin/drivers/{id}/approve', d: '204 and an approval e-mail', who: 'Admin',
          note: 'Only drivers in PendingReview can be decided (409 admin.not_pending_review otherwise).' },
        { id: 'a6', c: 1, r: 4, m: 'POST', p: '/admin/drivers/{id}/reject', d: '204 and a rejection e-mail', who: 'Admin',
          send: '{ "reason": "CNH photo is unreadable" }',
          note: 'The reason is required and goes by e-mail. The driver edits the profile and submits again.' },
        { id: 'a7', c: 1, r: 0, k: 'soft', m: 'POST', p: '/admin/terms', d: 'Publish a new term version', who: 'Admin',
          send: '{ "type": "TermsOfUse",\n  "version": "1.1",\n  "title": "Terms of use",\n  "contentUrl": "https://..." }',
          note: 'Deactivates the previous version of the same type. Every user must accept again.' }
      ],
      edges: [
        { a: 'a1', b: 'a2' }, { a: 'a2', b: 'a3' }, { a: 'a3', b: 'a4' },
        { a: 'a4', b: 'a5', label: 'approve' }, { a: 'a4', b: 'a6', label: 'reject' },
        { a: 'a1', b: 'a7', dashed: true, label: 'new terms' }
      ],
      seq: ['a1', 'a2', 'a3', 'a4', 'a5']
    },

    {
      id: 'token',
      label: 'Token renewal',
      intro: 'Any authenticated call can answer 401 when the access token expires. Renew once and repeat the call. Each refresh token works one time, so store the new pair.',
      nodes: [
        { id: 't1', c: 0, r: 0, t: 'Any authenticated call', d: 'Authorization: Bearer <accessToken>', who: 'Signed in' },
        { id: 't2', c: 0, r: 1, k: 'decision', t: 'Got 401?', d: 'The access token expired', who: 'Front-end' },
        { id: 't3', c: 0, r: 2, m: 'POST', p: '/auth/refresh', d: 'Send the stored refreshToken', who: 'Anyone',
          send: '{ "refreshToken": "..." }',
          get: '{ accessToken, refreshToken, ... }\n(same shape as login)',
          note: 'The old refresh token stops working. Sending a used one makes the server revoke the whole session.' },
        { id: 't4', c: 0, r: 3, k: 'decision', t: 'Got 200?', d: 'The refresh worked', who: 'Front-end' },
        { id: 't5', c: 0, r: 4, k: 'go', t: 'Repeat the call', d: 'Once, with the new access token', who: 'Signed in',
          note: 'Replace both stored tokens before repeating.' },
        { id: 't6', c: 1, r: 1, k: 'soft', t: 'Use the response', d: 'Nothing to renew', who: 'Front-end' },
        { id: 't7', c: 1, r: 3, k: 'soft', t: 'Back to sign-in', d: 'Clear the stored tokens', who: 'Front-end',
          note: 'The refresh token is expired, used or revoked.' }
      ],
      edges: [
        { a: 't1', b: 't2' }, { a: 't2', b: 't3', label: 'yes' }, { a: 't2', b: 't6', label: 'no' },
        { a: 't3', b: 't4' }, { a: 't4', b: 't5', label: 'yes' }, { a: 't4', b: 't7', label: 'no' }
      ],
      seq: ['t1', 't2', 't3', 't4', 't5']
    }
  ];


  /* ------------------------------------------------------ Portuguese texts */

  const WHO_PT = {
    'Anyone': 'Qualquer pessoa', 'Signed in': 'Logado', 'Driver': 'Motorista', 'Customer': 'Cliente',
    'Admin': 'Admin', 'Front-end': 'Front-end', 'Driver (approved)': 'Motorista (aprovado)'
  };

  const PT_FLOWS = {
    session: {
      label: 'Sessão',
      intro: 'Todo usuário começa aqui: lê os termos, abre uma sessão e aceita o que estiver pendente. Depois, abra a aba do perfil da conta.',
      edges: { 's4>s5': 'sim', 's5>s3': 'repetir', 's4>s6': 'não' },
      nodes: {
        s1: { d: 'Mostre os termos antes do cadastro', note: 'Não precisa de token. Deixe o usuário ler os textos antes de criar a conta.' },
        s2: { d: 'ou /auth/login. Guarde os dois tokens',
          send: '{ "fullName": "Maria Souza",\n  "email": "maria@example.com",\n  "phoneNumber": "+5511999999999",\n  "password": "Str0ng#Pass",\n  "role": "Customer" }   // ou "Driver"\n\nlogin: { "email", "password" }',
          note: 'Envie o access token em Authorization: Bearer. Ele dura 15 minutos; veja a aba de renovação. user.role diz qual fluxo mostrar.' },
        s3: { d: 'O que o usuário ainda precisa aceitar', note: 'Lista vazia significa que não falta nada. Uma nova versão publicada pelo admin aparece aqui de novo.' },
        s4: { t: 'Falta algum termo?', d: 'A lista não está vazia?', note: 'O cliente precisa de todos os termos ativos aceitos para criar um frete. O motorista precisa deles antes de enviar o perfil para análise.' },
        s5: { d: 'Uma chamada por termo', note: 'A data e o IP são gravados. Aceitar o mesmo termo duas vezes devolve o primeiro aceite.' },
        s6: { t: 'Abra o fluxo do perfil', d: 'Aba Motorista, Cliente ou Admin', note: 'Direcione o usuário por user.role da resposta de login ou cadastro.' }
      }
    },
    driver: {
      label: 'Motorista',
      cols: ['Cadastro', 'Propostas e corrida'],
      intro: 'O motorista só vê fretes depois que um admin aprova o perfil. Aprovado, a coluna da direita abre.',
      edges: { 'd6>d3': 'não', 'd6>d7': 'sim', 'd9>d2': 'Rejected', 'd9>d10': 'Approved', 'm3>m4': 'opcional', 'm3>m5': 'Accepted' },
      nodes: {
        d1: { d: '404 significa que ainda não há perfil', note: 'status é Draft, PendingReview, Approved ou Rejected. Um 404 (driver.profile_not_found) significa que a próxima chamada é o PUT.' },
        d2: { d: 'taxId (CPF ou CNPJ), CNH e categoria', get: '200 com o perfil (mesmo formato do GET)',
          note: 'taxId é obrigatório e pode ser CPF ou CNPJ (letras permitidas). É validado pelos dígitos antes de gravar. Só editável em Draft ou Rejected.' },
        d3: { d: 'Pelo menos um veículo ativo', note: 'type é Fiorino, Van, Pickup, Truck34, Truck ou Trailer. A placa aceita ABC1234 e ABC1D23.' },
        d4: { t: 'Envie o arquivo ao storage', d: 'Chave: drivers/{userId}/nome.jpg',
          note: 'Isso acontece fora da API. A chave precisa começar com drivers/{seu id de usuário}/. O endpoint de upload pré-assinado ainda não existe, então hoje o front-end só registra a chave.' },
        d5: { d: 'Registre o arquivo enviado' },
        d6: { d: 'Leia em GET /driver/profile', note: 'Pronto significa: termos aceitos, um veículo ativo e um documento. Se não, corrija o que falta e confira de novo.' },
        d7: { d: 'O status vira PendingReview', get: '200 com o perfil, status PendingReview',
          note: 'Falha com a lista de itens faltantes se o checklist estiver incompleto. O perfil fica travado até o admin decidir.' },
        d8: { d: 'Consulte até sair de PendingReview', note: 'Ainda não há push. A decisão do admin também envia um e-mail ao motorista.' },
        d9: { t: 'Status?', d: 'Approved ou Rejected', note: 'Rejected vem com rejectionReason. Mostre o motivo, deixe o motorista editar e enviar de novo.' },
        d10: { t: 'Approved: pode propor', d: 'Abre o quadro de fretes' },
        m1: { d: 'Fretes abertos que cabem nos seus veículos',
          note: 'Mostra quantas coletas e entregas a corrida tem e, de cada parada, a região e os itens. Só fretes dentro da janela de propostas cujo tipo de veículo bate com um dos seus veículos ativos (até 100). O endereço completo fica oculto até a sua proposta ser aprovada.' },
        m2: { d: 'Você define o preço', get: '201 com a proposta',
          note: 'Uma proposta pendente por frete. O veículo precisa estar ativo, ser do tipo pedido e comportar o peso. Frete do dia aceita propostas por 5 minutos; agendado, por 24 horas.' },
        m3: { d: 'Pending, Accepted, Rejected, Expired', note: 'Accepted significa que o cliente escolheu você e já existe uma entrega.' },
        m4: { d: 'Retire uma proposta pendente', note: 'Só enquanto a proposta estiver Pending.' },
        m5: { d: 'Entregas que você ganhou', note: 'Uma proposta que vira Accepted cria uma entrega aqui.' },
        m6: { d: 'Corrida inteira ou parada a parada',
          note: 'Agora com os endereços completos. ?view=Full devolve todas as coletas e entregas de uma vez, como visão geral da corrida. ?view=Next devolve só a primeira parada ainda não concluída, para o app mostrar uma parada depois da outra. Cada parada lista os itens a coletar ou entregar.' }
      }
    },
    customer: {
      label: 'Cliente',
      cols: ['Pedir um frete', 'À parte'],
      intro: 'O cliente publica o frete, vê as propostas chegarem ordenadas por preço e aprova uma. A aprovação cria a entrega.',
      edges: { 'c4>c5': 'aprovar', 'c4>c3': 'aguardar', 'c4>c6': 'cancelar', 'c2>c7': 'a qualquer hora' },
      nodes: {
        c1: { d: 'Todos os endereços de coleta e entrega, salvos uma vez', get: '201 com o endereço e seu id',
          note: 'Cadastre um endereço para cada coleta e cada entrega. O estado precisa ser uma UF válida e o CEP tem 8 dígitos. Os ids vão na próxima chamada. GET /addresses lista os salvos. O cliente pode ser pessoa ou empresa.' },
        c2: { d: 'Paradas em ordem: coletas e depois entregas',
          send: '{ "stops": [\n    { "type": "Pickup", "addressId": "3fa8...", "note": "Portão 2" },\n    { "type": "Pickup", "addressId": "9b1d..." },\n    { "type": "Delivery", "addressId": "7c9e...",\n      "items": [{ "description": "Pastilhas de freio", "quantity": 12 }] },\n    { "type": "Delivery", "addressId": "5d2a...", "note": "Final" } ],\n  "cargoDescription": "Autopeças",\n  "cargoWeightKg": 300,\n  "requiredVehicleType": "Fiorino",\n  "scheduledPickupAt": null }',
          get: '201 { id, status: "Open", proposalDeadline,\n  pickupCount, deliveryCount,\n  stops: [{ sequence, type, address, note,\n    items, completedAt }], ... }',
          note: 'A ordem do array stops é a ordem que o motorista segue. A primeira parada precisa ser Pickup e a última Delivery: é o endereço final de entrega. Os itens são opcionais e podem ir em coletas ou entregas. Todos os endereços precisam ser diferentes e seus. Sem scheduledPickupAt o frete é do dia e a janela de propostas é de 5 minutos; com uma data futura em UTC (terminando em Z) é de até 24 horas. 403 freight.terms_not_accepted quando falta algum termo.' },
        c3: { d: 'Ordenadas por preço, menor primeiro', note: 'Empate vai para a proposta mais antiga. Consulte enquanto o frete estiver Open.' },
        c4: { t: 'Escolher um motorista?', d: 'Compare preço e veículo' },
        c5: { d: '201: entrega criada',
          note: 'Cria a entrega, reserva a agenda do motorista (2 horas mais 1 hora por parada, a partir da primeira coleta), rejeita as outras propostas e fecha o frete. Aprove antes do fim da janela de propostas. 409 proposal.driver_busy significa que o motorista foi reservado nesse meio tempo: escolha outro.' },
        c6: { d: 'Desista de um frete aberto', note: 'Só enquanto o frete estiver Open. As propostas pendentes são rejeitadas.' },
        c7: { d: 'Situação dos seus fretes, a qualquer hora',
          note: 'Filtre com ?status=Open, Approved, Expired ou Cancelled. GET /freights/{id} devolve um só. Um frete pode continuar Open por até 30 segundos depois do fim da janela, até a rotina de expiração rodar.' }
      }
    },
    admin: {
      label: 'Admin',
      intro: 'O admin analisa os motoristas na fila. O motorista recebe um e-mail com a decisão.',
      edges: { 'a4>a5': 'aprovar', 'a4>a6': 'rejeitar', 'a1>a7': 'novos termos' },
      nodes: {
        a1: { d: 'Conta com perfil Admin', note: 'Em Development a API cria esse admin na primeira execução, usando Seed:Admin do appsettings.Development.json.' },
        a2: { d: 'Fila de análise, mais antigos primeiro', note: 'Por padrão mostra PendingReview. Filtre com ?status=.' },
        a3: { d: 'Perfil, veículos e arquivos' },
        a4: { t: 'Decisão', d: 'Aprovar ou rejeitar' },
        a5: { d: '204 e e-mail de aprovação', note: 'Só motoristas em PendingReview podem ser decididos (senão 409 admin.not_pending_review).' },
        a6: { d: '204 e e-mail de rejeição', send: '{ "reason": "A foto da CNH está ilegível" }', note: 'O motivo é obrigatório e vai por e-mail. O motorista edita o perfil e envia de novo.' },
        a7: { d: 'Publique uma nova versão de termo', note: 'Desativa a versão anterior do mesmo tipo. Todos os usuários precisam aceitar de novo.' }
      }
    },
    token: {
      label: 'Renovação do token',
      intro: 'Qualquer chamada autenticada pode responder 401 quando o access token expira. Renove uma vez e repita a chamada. Cada refresh token funciona uma vez, então guarde o novo par.',
      edges: { 't2>t3': 'sim', 't2>t6': 'não', 't4>t5': 'sim', 't4>t7': 'não' },
      nodes: {
        t1: { t: 'Qualquer chamada autenticada', d: 'Authorization: Bearer <accessToken>' },
        t2: { t: 'Veio 401?', d: 'O access token expirou' },
        t3: { d: 'Envie o refreshToken guardado', get: '{ accessToken, refreshToken, ... }\n(mesmo formato do login)',
          note: 'O refresh token antigo deixa de funcionar. Enviar um já usado faz o servidor revogar a sessão inteira.' },
        t4: { t: 'Veio 200?', d: 'A renovação funcionou' },
        t5: { t: 'Repita a chamada', d: 'Uma vez, com o novo access token', note: 'Troque os dois tokens guardados antes de repetir.' },
        t6: { t: 'Use a resposta', d: 'Nada a renovar' },
        t7: { t: 'Volte ao login', d: 'Apague os tokens guardados', note: 'O refresh token expirou, já foi usado ou foi revogado.' }
      }
    }
  };

  if (PT) {
    FLOWS.forEach((fl) => {
      const x = PT_FLOWS[fl.id];
      if (!x) return;
      fl.label = x.label;
      fl.intro = x.intro;
      if (x.cols) fl.cols = x.cols;
      fl.edges.forEach((e) => {
        const label = x.edges[e.a + '>' + e.b];
        if (label) e.label = label;
      });
      fl.nodes.forEach((n) => {
        const o = x.nodes[n.id];
        if (o) Object.assign(n, o);
        if (WHO_PT[n.who]) n.who = WHO_PT[n.who];
      });
    });
  }

  /* ----------------------------------------------------------------- state */

  const state = {
    flow: 0,
    step: -1,
    selected: null,
    hover: null,
    playing: false,
    anim: null,
    done: new Set(),
    layout: null,
    width: 0,
    dpr: 1
  };
  let timer = 0;
  let raf = 0;

  const flow = () => FLOWS[state.flow];
  const idxOf = (id) => flow().seq.indexOf(id);
  const edgeKey = (a, b) => a + '>' + b;

  /* ----------------------------------------------------------------- layout */

  function wrapText(text, maxW, font) {
    ctx.font = font;
    const words = String(text).split(' ');
    const lines = [];
    let line = '';
    for (const word of words) {
      const next = line ? line + ' ' + word : word;
      if (line && ctx.measureText(next).width > maxW) {
        lines.push(line);
        line = word;
      } else {
        line = next;
      }
    }
    if (line) lines.push(line);
    return lines;
  }

  function wrapPath(path, maxW, font) {
    ctx.font = font;
    const parts = path.split(/(?=\/)/);
    const lines = [];
    let line = '';
    for (const part of parts) {
      if (line && ctx.measureText(line + part).width > maxW) {
        lines.push(line);
        line = part;
      } else {
        line += part;
      }
    }
    if (line) lines.push(line);
    return lines;
  }

  function measureNode(n, w) {
    const px = n.k === 'decision' ? 26 : 14;
    const inner = Math.max(60, w - px * 2);
    const m = { px, inner, pill: 0, inline: false, title: [], desc: [], h: 0 };
    const pathFont = `700 13px ${F.mono}`;
    const titleFont = `700 13.5px ${F.disp}`;

    if (n.m) {
      ctx.font = `700 10.5px ${F.mono}`;
      m.pill = Math.ceil(ctx.measureText(n.m).width) + 16;
    }
    if (n.p) {
      ctx.font = pathFont;
      const pathW = ctx.measureText(n.p).width;
      if (n.m && m.pill + 8 + pathW <= inner) {
        m.inline = true;
        m.title = [n.p];
      } else {
        m.title = wrapPath(n.p, inner, pathFont);
      }
      m.titleFont = pathFont;
    } else {
      m.title = wrapText(n.t, inner, titleFont);
      m.titleFont = titleFont;
    }
    m.desc = n.d ? wrapText(n.d, inner, `500 12.5px ${F.body}`) : [];

    let h = 12;
    if (n.m && !m.inline) h += 22;
    h += m.inline ? 22 : m.title.length * 18;
    if (m.desc.length) h += 4 + m.desc.length * 17;
    h += 12;
    m.h = Math.max(h, n.k === 'decision' ? 60 : 52);
    return m;
  }

  function computeLayout(fl, W) {
    const pad = 16;
    const headerH = fl.cols ? 34 : 0;
    const gapY = 56;
    let cols = Math.max(...fl.nodes.map((n) => n.c)) + 1;
    let gl = 44, gr = 44, gap = 44;
    let wide = cols > 1;
    let nodeW = 0;

    if (wide) {
      nodeW = Math.floor((W - 2 * pad - (cols - 1) * gap) / cols) - gl - gr;
      if (nodeW < 210) wide = false;
    }
    if (!wide) {
      cols = 1;
      gl = gr = W < 420 ? 30 : 38;
      nodeW = W - 2 * pad - gl - gr;
    }
    nodeW = Math.max(150, Math.min(nodeW, 350));

    const colW = nodeW + gl + gr;
    const blockW = cols * colW + (cols - 1) * gap;
    const x0 = Math.max(pad, Math.floor((W - blockW) / 2));

    const map = {};
    const list = fl.nodes.map((n, i) => {
      const col = wide ? n.c : 0;
      const row = wide ? n.r : i;
      const m = measureNode(n, nodeW);
      const item = { n, col, row, w: nodeW, h: m.h, m, x: x0 + col * (colW + gap) + gl, y: 0 };
      map[n.id] = item;
      return item;
    });

    const rows = Math.max(...list.map((o) => o.row)) + 1;
    const rowH = new Array(rows).fill(0);
    list.forEach((o) => { rowH[o.row] = Math.max(rowH[o.row], o.h); });
    const rowY = [];
    let y = pad + headerH;
    for (let r = 0; r < rows; r++) {
      rowY[r] = y;
      y += rowH[r] + (r < rows - 1 ? gapY : 0);
    }
    list.forEach((o) => { o.y = Math.round(rowY[o.row] + (rowH[o.row] - o.h) / 2); });

    const L = { W, H: y + pad + 6, wide, cols, colW, gap, gl, gr, x0, headerH, rowY, rowH, gapY, map, list, edges: [] };
    L.edges = fl.edges.map((e) => routeEdge(e, L));
    return L;
  }

  function routeEdge(e, L) {
    const A = L.map[e.a];
    const B = L.map[e.b];
    const acx = A.x + A.w / 2, bcx = B.x + B.w / 2;
    const acy = A.y + A.h / 2, bcy = B.y + B.h / 2;
    const out = { e, pts: [], label: null, kind: 'down', loop: false };

    if (A.row === B.row && A.col !== B.col) {
      const right = B.col > A.col;
      const sx = right ? A.x + A.w : A.x;
      const ex = right ? B.x : B.x + B.w;
      out.pts = [[sx, acy], [ex, bcy]];
      out.label = { x: (sx + ex) / 2, y: acy, rot: false };
      out.kind = 'side';
    } else if (A.col === B.col) {
      const blocked = L.list.some((o) => o !== A && o !== B && o.col === A.col &&
        o.row > Math.min(A.row, B.row) && o.row < Math.max(A.row, B.row));
      if (B.row > A.row && !blocked) {
        const y1 = A.y + A.h;
        out.pts = [[acx, y1], [acx, B.y]];
        out.label = { x: acx + 10, y: (y1 + B.y) / 2, rot: false, left: true };
      } else if (B.row > A.row) {
        const k = slot(L, 'r' + A.col, A.row, B.row);
        const gx = A.x + A.w + 16 + 12 * k;
        out.pts = [[A.x + A.w, acy], [gx, acy], [gx, bcy], [B.x + B.w, bcy]];
        out.label = { x: gx, y: (acy + bcy) / 2, rot: true };
        out.kind = 'skip';
      } else {
        const k = slot(L, 'l' + A.col, B.row, A.row);
        const gx = A.x - 16 - 12 * k;
        out.pts = [[A.x, acy], [gx, acy], [gx, bcy], [B.x, bcy]];
        out.label = { x: gx, y: (acy + bcy) / 2, rot: true };
        out.kind = 'loop';
        out.loop = true;
      }
    } else if (B.row > A.row) {
      const y1 = A.y + A.h;
      const barY = L.rowY[A.row] + L.rowH[A.row] + L.gapY / 2;
      out.pts = [[acx, y1], [acx, barY], [bcx, barY], [bcx, B.y]];
      out.label = { x: (acx + bcx) / 2, y: barY, rot: false };
    } else {
      const goLeft = B.col < A.col;
      const gx = goLeft
        ? L.x0 + A.col * (L.colW + L.gap) - L.gap / 2
        : L.x0 + A.col * (L.colW + L.gap) + L.colW + L.gap / 2;
      const sx = goLeft ? A.x : A.x + A.w;
      const ex = goLeft ? B.x + B.w : B.x;
      out.pts = [[sx, acy], [gx, acy], [gx, bcy], [ex, bcy]];
      out.label = { x: gx, y: (acy + bcy) / 2, rot: true };
      out.kind = 'loop';
      out.loop = true;
    }

    out.len = 0;
    out.segs = [];
    for (let i = 1; i < out.pts.length; i++) {
      const l = Math.hypot(out.pts[i][0] - out.pts[i - 1][0], out.pts[i][1] - out.pts[i - 1][1]);
      out.segs.push(l);
      out.len += l;
    }
    return out;
  }

  // Edges that share a gutter get parallel lanes when their row spans overlap.
  function slot(L, key, from, to) {
    L.slots = L.slots || {};
    const used = (L.slots[key] = L.slots[key] || []);
    let k = 0;
    while (used.some((u) => u.k === k && u.from <= to && from <= u.to)) k++;
    used.push({ k, from, to });
    return k;
  }

  function pointAt(re, t) {
    let d = re.len * t;
    for (let i = 0; i < re.segs.length; i++) {
      if (d <= re.segs[i] || i === re.segs.length - 1) {
        const f = re.segs[i] === 0 ? 0 : Math.min(1, d / re.segs[i]);
        const p = re.pts[i], q = re.pts[i + 1];
        return [p[0] + (q[0] - p[0]) * f, p[1] + (q[1] - p[1]) * f];
      }
      d -= re.segs[i];
    }
    return re.pts[re.pts.length - 1];
  }

  /* ---------------------------------------------------------------- drawing */

  function roundRect(x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }

  function hexagon(x, y, w, h) {
    const c = Math.min(18, h / 2);
    ctx.beginPath();
    ctx.moveTo(x + c, y);
    ctx.lineTo(x + w - c, y);
    ctx.lineTo(x + w, y + h / 2);
    ctx.lineTo(x + w - c, y + h);
    ctx.lineTo(x + c, y + h);
    ctx.lineTo(x, y + h / 2);
    ctx.closePath();
  }

  function shape(o, grow) {
    const g = grow || 0;
    if (o.n.k === 'decision') hexagon(o.x - g, o.y - g, o.w + g * 2, o.h + g * 2);
    else roundRect(o.x - g, o.y - g, o.w + g * 2, o.h + g * 2, 14 + g);
  }

  function arrowHead(x, y, dx, dy, color) {
    const len = Math.hypot(dx, dy) || 1;
    const ux = dx / len, uy = dy / len;
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(x - ux * 10 - uy * 5.5, y - uy * 10 + ux * 5.5);
    ctx.lineTo(x - ux * 10 + uy * 5.5, y - uy * 10 - ux * 5.5);
    ctx.closePath();
    ctx.fill();
  }

  function strokePts(pts, upTo) {
    ctx.beginPath();
    ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);
    if (upTo) ctx.lineTo(upTo[0], upTo[1]);
  }

  function partial(re, t) {
    let d = re.len * t;
    const pts = [re.pts[0]];
    for (let i = 0; i < re.segs.length; i++) {
      if (d >= re.segs[i]) {
        pts.push(re.pts[i + 1]);
        d -= re.segs[i];
      } else {
        const f = d / re.segs[i];
        const p = re.pts[i], q = re.pts[i + 1];
        pts.push([p[0] + (q[0] - p[0]) * f, p[1] + (q[1] - p[1]) * f]);
        break;
      }
    }
    return pts;
  }

  function drawEdge(re, progress) {
    const key = edgeKey(re.e.a, re.e.b);
    const walked = state.done.has(key);
    const base = re.e.dashed ? C.grey : (re.loop ? C.red : C.ink);
    const color = walked ? C.red : base;
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    ctx.strokeStyle = color;
    ctx.lineWidth = walked ? 2.8 : 1.8;
    ctx.setLineDash(re.e.dashed ? [3, 6] : (re.loop && !walked ? [7, 5] : []));
    strokePts(re.pts);
    ctx.stroke();
    ctx.setLineDash([]);
    const n = re.pts.length;
    arrowHead(re.pts[n - 1][0], re.pts[n - 1][1],
      re.pts[n - 1][0] - re.pts[n - 2][0], re.pts[n - 1][1] - re.pts[n - 2][1], color);

    if (progress !== null) {
      const pts = partial(re, progress);
      ctx.strokeStyle = C.red;
      ctx.lineWidth = 3.4;
      strokePts(pts);
      ctx.stroke();
    }
  }

  function drawLabel(re) {
    const lab = re.label;
    if (!re.e.label || !lab) return;
    ctx.font = `700 11px ${F.body}`;
    const tw = ctx.measureText(re.e.label).width;
    const w = tw + 14, h = 18;
    const walked = state.done.has(edgeKey(re.e.a, re.e.b));
    ctx.save();
    ctx.translate(lab.x, lab.y);
    if (lab.rot) ctx.rotate(-Math.PI / 2);
    const x = lab.left ? 0 : -w / 2;
    roundRect(x, -h / 2, w, h, 9);
    ctx.fillStyle = C.white;
    ctx.fill();
    ctx.lineWidth = 1.2;
    ctx.strokeStyle = walked || re.loop ? C.red : C.ink;
    ctx.stroke();
    ctx.fillStyle = walked || re.loop ? C.redDeep : C.ink;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(re.e.label, x + w / 2, 0.5);
    ctx.restore();
  }

  function isVisited(id) {
    const i = idxOf(id);
    if (i < 0) return false;
    if (state.anim && i === state.step && state.anim.pending) return false;
    return i <= state.step || flow().seq.slice(0, state.step + 1).includes(id);
  }

  function isCurrent(id) {
    if (state.step < 0) return false;
    if (state.anim && state.anim.pending) return false;
    return flow().seq[state.step] === id;
  }

  function drawNode(o) {
    const n = o.n;
    const m = o.m;
    const visited = isVisited(n.id);
    const current = isCurrent(n.id);
    const selected = state.selected === n.id;
    const hover = state.hover === n.id;
    const inSeq = idxOf(n.id) >= 0;
    const dim = state.step >= 0 && inSeq && !visited;
    const go = n.k === 'go';

    ctx.save();
    ctx.globalAlpha = dim ? 0.5 : 1;

    if (current) {
      ctx.shadowColor = 'rgba(215,20,26,.55)';
      ctx.shadowBlur = 18;
    }
    shape(o);
    ctx.fillStyle = go ? C.red : (visited ? C.tint : (n.k === 'soft' || n.k === 'decision' ? C.panel : C.white));
    ctx.fill();
    ctx.shadowBlur = 0;
    ctx.shadowColor = 'transparent';

    ctx.lineWidth = hover || current ? 2.6 : 1.7;
    ctx.strokeStyle = go ? C.redDeep : (visited || current ? C.red : C.ink);
    ctx.setLineDash(n.k === 'soft' ? [6, 4] : []);
    ctx.stroke();
    ctx.setLineDash([]);

    if (selected) {
      shape(o, 5);
      ctx.lineWidth = 2;
      ctx.strokeStyle = C.red;
      ctx.stroke();
    }

    // text
    const tx = o.x + m.px;
    let ty = o.y + 12;
    const center = n.k === 'decision';
    const fg = go ? C.white : C.ink;
    const sub = go ? 'rgba(255,255,255,.9)' : C.soft;
    ctx.textBaseline = 'middle';
    ctx.textAlign = center ? 'center' : 'left';
    const cx = o.x + o.w / 2;

    if (n.m) {
      ctx.font = `700 10.5px ${F.mono}`;
      const post = n.m === 'POST' || n.m === 'DELETE';
      const fill = go ? C.white : (post ? C.red : (n.m === 'GET' ? C.white : C.ink));
      const text = go ? C.red : (n.m === 'GET' ? C.ink : C.white);
      roundRect(tx, ty + 1, m.pill, 20, 10);
      ctx.fillStyle = fill;
      ctx.fill();
      if (n.m === 'GET' && !go) {
        ctx.lineWidth = 1.3;
        ctx.strokeStyle = C.ink;
        ctx.stroke();
      }
      ctx.fillStyle = text;
      ctx.textAlign = 'center';
      ctx.fillText(n.m, tx + m.pill / 2, ty + 11.5);
      ctx.textAlign = 'left';
      if (m.inline) {
        ctx.font = m.titleFont;
        ctx.fillStyle = fg;
        ctx.fillText(m.title[0], tx + m.pill + 8, ty + 11.5);
        ty += 22;
      } else {
        ty += 22;
      }
    }
    if (!(n.m && m.inline)) {
      ctx.font = m.titleFont;
      ctx.fillStyle = fg;
      ctx.textAlign = center ? 'center' : 'left';
      m.title.forEach((line) => {
        ctx.fillText(line, center ? cx : tx, ty + 9);
        ty += 18;
      });
    }
    if (m.desc.length) {
      ty += 4;
      ctx.font = `500 12.5px ${F.body}`;
      ctx.fillStyle = sub;
      m.desc.forEach((line) => {
        ctx.fillText(line, center ? cx : tx, ty + 8);
        ty += 17;
      });
    }
    ctx.restore();

    // step number badge
    const si = idxOf(n.id);
    if (si >= 0) {
      const first = flow().seq.indexOf(n.id);
      const bx = o.x - 2, by = o.y - 2;
      ctx.beginPath();
      ctx.arc(bx, by, 11, 0, Math.PI * 2);
      ctx.fillStyle = visited || current ? C.red : (go ? C.white : C.ink);
      ctx.fill();
      ctx.lineWidth = 2;
      ctx.strokeStyle = C.white;
      ctx.stroke();
      ctx.font = `700 11px ${F.disp}`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillStyle = (go && !(visited || current)) ? C.red : C.white;
      ctx.fillText(String(first + 1), bx, by + 0.5);
    }
  }

  function draw(now) {
    const L = state.layout;
    if (!L) return;
    ctx.setTransform(state.dpr, 0, 0, state.dpr, 0, 0);
    ctx.clearRect(0, 0, L.W, L.H);

    // column titles
    const fl = flow();
    if (fl.cols && L.wide) {
      ctx.font = `700 12px ${F.disp}`;
      ctx.textBaseline = 'middle';
      ctx.textAlign = 'left';
      fl.cols.forEach((title, c) => {
        const x = L.x0 + c * (L.colW + L.gap) + L.gl;
        ctx.fillStyle = C.red;
        ctx.fillRect(x, 16, 22, 4);
        ctx.fillStyle = C.ink;
        ctx.fillText(title.toUpperCase(), x + 30, 19);
      });
    }

    let progress = null;
    let animEdge = null;
    if (state.anim) {
      progress = Math.min(1, Math.max(0, (now - state.anim.t0) / state.anim.dur));
      const t = progress < 0.5 ? 2 * progress * progress : 1 - Math.pow(-2 * progress + 2, 2) / 2;
      progress = t;
      animEdge = state.anim.re;
    }

    L.edges.forEach((re) => drawEdge(re, re === animEdge ? progress : null));
    L.edges.forEach(drawLabel);
    L.list.forEach(drawNode);

    if (animEdge) {
      const p = pointAt(animEdge, progress);
      ctx.save();
      ctx.shadowColor = 'rgba(215,20,26,.7)';
      ctx.shadowBlur = 14;
      ctx.beginPath();
      ctx.arc(p[0], p[1], 7, 0, Math.PI * 2);
      ctx.fillStyle = C.red;
      ctx.fill();
      ctx.shadowBlur = 0;
      ctx.lineWidth = 2.5;
      ctx.strokeStyle = C.white;
      ctx.stroke();
      ctx.restore();
    }
  }

  function frame(now) {
    raf = 0;
    if (state.anim && now - state.anim.t0 >= state.anim.dur) {
      const finish = state.anim.onDone;
      state.anim = null;
      finish();
    }
    draw(now);
    if (state.anim) requestDraw();
  }

  function requestDraw() {
    if (!raf) raf = requestAnimationFrame(frame);
  }

  /* ---------------------------------------------------------------- sizing */

  function relayout() {
    const W = Math.max(280, Math.floor(stage.clientWidth));
    state.width = W;
    state.dpr = Math.min(window.devicePixelRatio || 1, 3);
    state.layout = computeLayout(flow(), W);
    canvas.width = Math.round(W * state.dpr);
    canvas.height = Math.round(state.layout.H * state.dpr);
    canvas.style.height = state.layout.H + 'px';
    requestDraw();
  }

  /* --------------------------------------------------------------- controls */

  const els = {
    tabs: document.getElementById('flow-tabs'),
    intro: document.getElementById('flow-intro'),
    play: document.getElementById('flow-play'),
    back: document.getElementById('flow-back'),
    next: document.getElementById('flow-next'),
    reset: document.getElementById('flow-reset'),
    step: document.getElementById('flow-step'),
    detail: document.getElementById('flow-detail'),
    text: document.getElementById('flow-text')
  };

  function h(tag, cls, text) {
    const el = document.createElement(tag);
    if (cls) el.className = cls;
    if (text !== undefined) el.textContent = text;
    return el;
  }

  function nodeLabel(n) {
    return (n.m ? n.m + ' ' : '') + (n.p || n.t);
  }

  function renderDetail(id) {
    const box = els.detail;
    box.replaceChildren();
    const n = id ? state.layout.map[id].n : null;
    if (!n) {
      box.append(h('p', 'fd-empty', tr('Select a step to see what to send, what comes back and what to watch for. Or press Play to walk the main path.', 'Selecione um passo para ver o que enviar, o que volta e o que observar. Ou aperte Play para percorrer o caminho principal.')));
      return;
    }
    const head = h('div', 'fd-head');
    if (n.m) head.append(h('span', 'fd-m ' + n.m.toLowerCase(), n.m));
    head.append(h('span', 'fd-path', n.p || n.t));
    if (n.who) head.append(h('span', 'fd-who', n.who));
    box.append(head);
    if (n.note) box.append(h('p', 'fd-note', n.note));
    if (n.send || n.get) {
      const grid = h('div', 'fd-grid');
      [[tr('Send', 'Enviar'), n.send], [tr('Receive', 'Receber'), n.get]].forEach(([title, body]) => {
        if (!body) return;
        const col = h('div', 'fd-col');
        col.append(h('h4', null, title), h('pre', 'code', body));
        grid.append(col);
      });
      box.append(grid);
    }
  }

  function updateUI() {
    const fl = flow();
    const last = fl.seq.length - 1;
    els.play.textContent = state.playing ? tr('Pause', 'Pausar') : (state.step >= last ? tr('Replay', 'Repetir') : (state.step < 0 ? 'Play' : tr('Resume', 'Continuar')));
    els.play.setAttribute('aria-pressed', String(state.playing));
    els.back.disabled = state.step < 0;
    els.next.disabled = state.step >= last;
    els.reset.disabled = state.step < 0 && !state.selected;
    if (state.step >= 0) {
      const n = state.layout.map[fl.seq[state.step]].n;
      els.step.textContent = tr(`Step ${state.step + 1} of ${fl.seq.length}: ${nodeLabel(n)}`, `Passo ${state.step + 1} de ${fl.seq.length}: ${nodeLabel(n)}`);
    } else {
      els.step.textContent = tr(`${fl.seq.length} steps on the main path`, `${fl.seq.length} passos no caminho principal`);
    }
    [...els.tabs.children].forEach((b, i) => {
      const on = i === state.flow;
      b.setAttribute('aria-selected', String(on));
      b.tabIndex = on ? 0 : -1;
    });
    canvas.setAttribute('aria-label', tr(`Call order diagram, ${fl.label}. ${els.step.textContent}. Use the arrow keys to move between steps.`, `Diagrama da ordem das chamadas, ${fl.label}. ${els.step.textContent}. Use as setas do teclado para mudar de passo.`));
  }

  function select(id) {
    state.selected = id;
    renderDetail(id);
  }

  function rebuildDone(upTo) {
    const seq = flow().seq;
    state.done = new Set();
    for (let i = 1; i <= upTo; i++) state.done.add(edgeKey(seq[i - 1], seq[i]));
  }

  function findRoute(a, b) {
    return state.layout.edges.find((re) => re.e.a === a && re.e.b === b) || null;
  }

  function stopTimer() {
    clearTimeout(timer);
    timer = 0;
  }

  function goTo(i, animate) {
    const seq = flow().seq;
    const target = Math.max(-1, Math.min(seq.length - 1, i));
    const prev = state.step;
    state.anim = null;
    state.step = target;
    rebuildDone(target);

    const arrive = () => {
      if (state.step >= 0) select(seq[state.step]); else select(null);
      updateUI();
      requestDraw();
    };

    if (animate && !reduceMotion && target > 0 && target === prev + 1) {
      const re = findRoute(seq[target - 1], seq[target]);
      if (re) {
        state.done.delete(edgeKey(seq[target - 1], seq[target]));
        const dur = Math.max(450, Math.min(1400, re.len / 0.5));
        state.anim = {
          re, t0: performance.now(), dur, pending: true,
          onDone: () => {
            state.done.add(edgeKey(seq[target - 1], seq[target]));
            state.anim = null;
            arrive();
          }
        };
        updateUI();
        requestDraw();
        return dur;
      }
    }
    arrive();
    return 0;
  }

  function pause() {
    state.playing = false;
    stopTimer();
  }

  function advance() {
    stopTimer();
    if (!state.playing) return;
    const last = flow().seq.length - 1;
    if (state.step >= last) {
      state.playing = false;
      updateUI();
      return;
    }
    const dur = goTo(state.step + 1, true);
    updateUI();
    timer = setTimeout(advance, dur + (reduceMotion ? 1800 : 1200));
  }

  function togglePlay() {
    if (state.playing) {
      pause();
      updateUI();
      return;
    }
    if (state.step >= flow().seq.length - 1) goTo(-1, false);
    state.playing = true;
    advance();
  }

  function stepBy(delta) {
    pause();
    if (state.anim) {
      const done = state.anim.onDone;
      state.anim = null;
      done();
    }
    goTo(state.step + delta, delta > 0);
  }

  function resetFlow() {
    pause();
    goTo(-1, false);
    select(null);
    updateUI();
  }

  function setFlow(i) {
    pause();
    state.flow = i;
    state.step = -1;
    state.selected = null;
    state.hover = null;
    state.anim = null;
    state.done = new Set();
    els.intro.textContent = flow().intro;
    relayout();
    renderDetail(null);
    buildText();
    updateUI();
  }

  function buildText() {
    els.text.replaceChildren();
    flow().seq.forEach((id) => {
      const n = state.layout.map[id].n;
      els.text.append(h('li', null, `${nodeLabel(n)}${n.d ? ': ' + n.d : ''}`));
    });
  }

  /* ------------------------------------------------------------ pointer/keys */

  function hit(ev) {
    const rect = canvas.getBoundingClientRect();
    const x = (ev.clientX - rect.left) * (state.width / rect.width);
    const y = (ev.clientY - rect.top) * (state.layout.H / rect.height);
    const list = state.layout.list;
    for (let i = list.length - 1; i >= 0; i--) {
      const o = list[i];
      if (x >= o.x && x <= o.x + o.w && y >= o.y && y <= o.y + o.h) return o.n.id;
    }
    return null;
  }

  canvas.addEventListener('pointermove', (ev) => {
    const id = hit(ev);
    if (id !== state.hover) {
      state.hover = id;
      canvas.style.cursor = id ? 'pointer' : 'default';
      requestDraw();
    }
  });
  canvas.addEventListener('pointerleave', () => {
    if (state.hover) {
      state.hover = null;
      requestDraw();
    }
  });
  canvas.addEventListener('click', (ev) => {
    const id = hit(ev);
    if (!id) return;
    pause();
    const i = idxOf(id);
    if (i >= 0) {
      goTo(i, false);
    } else {
      select(id);
      updateUI();
      requestDraw();
    }
  });
  canvas.addEventListener('keydown', (ev) => {
    switch (ev.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        ev.preventDefault();
        stepBy(1);
        break;
      case 'ArrowLeft':
      case 'ArrowUp':
        ev.preventDefault();
        stepBy(-1);
        break;
      case ' ':
      case 'Enter':
        ev.preventDefault();
        togglePlay();
        break;
      case 'Home':
      case 'Escape':
        ev.preventDefault();
        resetFlow();
        break;
      default:
    }
  });

  els.play.addEventListener('click', togglePlay);
  els.back.addEventListener('click', () => stepBy(-1));
  els.next.addEventListener('click', () => stepBy(1));
  els.reset.addEventListener('click', resetFlow);

  /* ------------------------------------------------------------------- boot */

  FLOWS.forEach((f, i) => {
    const b = h('button', 'flow-tab', f.label);
    b.type = 'button';
    b.setAttribute('role', 'tab');
    b.addEventListener('click', () => setFlow(i));
    b.addEventListener('keydown', (ev) => {
      if (ev.key !== 'ArrowRight' && ev.key !== 'ArrowLeft') return;
      ev.preventDefault();
      const next = (i + (ev.key === 'ArrowRight' ? 1 : FLOWS.length - 1)) % FLOWS.length;
      setFlow(next);
      els.tabs.children[next].focus();
    });
    els.tabs.append(b);
  });

  if ('ResizeObserver' in window) {
    let lastW = 0;
    new ResizeObserver(() => {
      const w = Math.floor(stage.clientWidth);
      if (w && w !== lastW) {
        lastW = w;
        relayout();
      }
    }).observe(stage);
  } else {
    window.addEventListener('resize', relayout);
  }

  setFlow(0);
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(relayout);
})();
