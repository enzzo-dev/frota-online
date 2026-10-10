(() => {
  'use strict';

  const PT = document.documentElement.lang.toLowerCase().startsWith('pt');
  const tr = (en, pt) => (PT ? pt : en);

  const STORE_KEY = 'frota.docs.session';
  const STATUS_TEXT = {
    200: 'OK', 201: 'Created', 204: 'No Content', 400: 'Bad Request', 401: 'Unauthorized',
    403: 'Forbidden', 404: 'Not Found', 409: 'Conflict', 500: 'Server Error'
  };
  const FIELD_NOTES = {
    fullName: tr('Up to 150 characters.', 'Até 150 caracteres.'),
    email: tr('Up to 254 characters. Saved in lower case.', 'Até 254 caracteres. Gravado em minúsculas.'),
    phoneNumber: tr('10 to 15 digits, with an optional leading +.', 'De 10 a 15 dígitos, com + opcional no início.'),
    password: tr('8 to 128 characters with an upper case letter, a lower case letter and a digit.', 'De 8 a 128 caracteres, com letra maiúscula, minúscula e um dígito.'),
    role: tr('Customer or Driver. Admin accounts cannot be self-registered.', 'Customer ou Driver. Contas Admin não se cadastram sozinhas.'),
    refreshToken: tr('The refresh token from your last login or refresh.', 'O refresh token do seu último login ou renovação.'),
    accessToken: tr('Send it as Authorization: Bearer <token>.', 'Envie como Authorization: Bearer <token>.')
  };

  const $ = (selector, root = document) => root.querySelector(selector);
  const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));

  /** Tiny DOM builder. Text is always inserted as text, never as HTML. */
  function h(tag, attrs, ...children) {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(attrs || {})) {
      if (value === false || value == null) continue;
      if (key === 'class') node.className = value;
      else if (key.startsWith('on') && typeof value === 'function') node.addEventListener(key.slice(2), value);
      else node.setAttribute(key, value === true ? '' : value);
    }
    const append = (child) => {
      if (child == null || child === false) return;
      if (Array.isArray(child)) child.forEach(append);
      else node.append(child.nodeType ? child : document.createTextNode(String(child)));
    };
    children.forEach(append);
    return node;
  }

  /* ---------- Session ---------- */

  let memorySession = null;

  function loadSession() {
    try {
      const raw = localStorage.getItem(STORE_KEY);
      return raw ? JSON.parse(raw) : memorySession;
    } catch { return memorySession; }
  }

  function saveSession(session) {
    memorySession = session;
    try {
      if (session) localStorage.setItem(STORE_KEY, JSON.stringify(session));
      else localStorage.removeItem(STORE_KEY);
    } catch { /* storage unavailable: the in-memory copy still works for this page */ }
    renderSession();
  }

  function sessionFromAuth(auth, previousRefresh) {
    return {
      accessToken: auth.accessToken,
      accessTokenExpiresAt: auth.accessTokenExpiresAt,
      refreshToken: auth.refreshToken,
      previousRefresh: previousRefresh || null,
      user: auth.user
    };
  }

  function minutesLeft(session) {
    return Math.round((new Date(session.accessTokenExpiresAt).getTime() - Date.now()) / 60000);
  }

  function renderSession() {
    const session = loadSession();
    const box = $('#session-box');
    const line = $('#session-line');
    const sub = $('#session-sub');
    const clear = $('#clear-session');

    if (!session || !session.user) {
      box.classList.remove('live');
      line.textContent = tr('Not signed in', 'Não conectado');
      sub.textContent = tr('Create an account in step 1 to get a token.', 'Crie uma conta na etapa 1 para obter um token.');
      clear.hidden = true;
    } else {
      const left = minutesLeft(session);
      box.classList.add('live');
      line.textContent = `${session.user.fullName} (${session.user.role})`;
      sub.textContent = left > 0
        ? tr(`${session.user.email}. Access token expires in ${left} min.`, `${session.user.email}. O access token expira em ${left} min.`)
        : tr(`${session.user.email}. Access token expired, refresh it in step 4.`, `${session.user.email}. O access token expirou, renove-o na etapa 4.`);
      clear.hidden = false;
    }
    renderCurl();
  }

  /* ---------- HTTP ---------- */

  async function call(method, path, body, withAuth) {
    const headers = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    const session = loadSession();
    if (withAuth && session && session.accessToken) headers.Authorization = `Bearer ${session.accessToken}`;

    const started = performance.now();
    try {
      const response = await fetch(path, {
        method, headers, body: body !== undefined ? JSON.stringify(body) : undefined
      });
      const text = await response.text();
      let data = text;
      try { data = text ? JSON.parse(text) : null; } catch { /* keep raw text */ }
      return {
        ok: response.ok, status: response.status,
        statusText: response.statusText || STATUS_TEXT[response.status] || '',
        data, ms: Math.round(performance.now() - started)
      };
    } catch {
      return {
        ok: false, status: 0, statusText: tr('No response', 'Sem resposta'),
        data: { detail: tr('The server could not be reached. Check that the API is running and try again.', 'Não foi possível acessar o servidor. Confira se a API está rodando e tente de novo.') },
        ms: Math.round(performance.now() - started)
      };
    }
  }

  function showResult(container, res, note) {
    container.hidden = false;
    container.className = `result ${res.ok ? 'ok' : 'fail'}`;
    const parts = [
      h('div', { class: 'result-head' },
        h('span', {}, res.status ? `${res.status} ${res.statusText}` : res.statusText),
        h('span', { class: 'ms' }, `${res.ms} ms`)),
      h('pre', {}, res.data == null || res.data === '' ? '(empty response)'
        : typeof res.data === 'string' ? res.data : JSON.stringify(res.data, null, 2))
    ];
    if (note) parts.push(h('p', { class: 'note' }, note));
    container.replaceChildren(...parts);
  }

  function markDone(stopId) {
    const stop = document.getElementById(stopId);
    if (stop) stop.classList.add('done');
  }

  function busy(button, work) {
    button.disabled = true;
    return work().finally(() => { button.disabled = false; });
  }

  /* ---------- Guide ---------- */

  function formData(form) {
    return Object.fromEntries(new FormData(form).entries());
  }

  function renderCurl() {
    const session = loadSession();
    const token = session && session.accessToken ? session.accessToken : '<access token>';
    const target = $('#curl-3');
    if (target) {
      target.textContent = `curl ${location.origin}/api/v1/auth/me \\\n  -H "Authorization: Bearer ${token}"`;
    }
  }

  function setupGuide() {
    const randomId = Math.random().toString(36).slice(2, 7);
    const registerForm = $('#form-register');
    const loginForm = $('#form-login');
    registerForm.elements.email.value = `driver.${randomId}@example.com`;
    loginForm.elements.email.value = registerForm.elements.email.value;
    registerForm.elements.email.addEventListener('input', () => {
      loginForm.elements.email.value = registerForm.elements.email.value;
    });

    registerForm.addEventListener('submit', (event) => {
      event.preventDefault();
      const button = $('button[type=submit]', registerForm);
      busy(button, async () => {
        const res = await call('POST', '/api/v1/auth/register', formData(registerForm), false);
        let note = null;
        if (res.ok) {
          saveSession(sessionFromAuth(res.data));
          markDone('stop-1');
          note = tr('Account created and signed in. Your tokens are saved for the next stops.', 'Conta criada e login feito. Seus tokens foram guardados para as próximas etapas.');
        } else if (res.status === 409) {
          note = tr('That e-mail already has an account. Change it above or continue to step 2 to sign in.', 'Esse e-mail já tem conta. Troque-o acima ou siga para a etapa 2 e entre.');
        } else if (res.status === 400) {
          note = tr('Fix the fields listed under "errors" and send again.', 'Corrija os campos listados em "errors" e envie de novo.');
        }
        showResult($('#result-1'), res, note);
      });
    });

    loginForm.addEventListener('submit', (event) => {
      event.preventDefault();
      const button = $('button[type=submit]', loginForm);
      busy(button, async () => {
        const res = await call('POST', '/api/v1/auth/login', formData(loginForm), false);
        let note = null;
        if (res.ok) {
          saveSession(sessionFromAuth(res.data));
          markDone('stop-2');
          note = tr('Signed in. A new token pair replaced the previous one.', 'Login feito. Um novo par de tokens substituiu o anterior.');
        } else if (res.status === 401) {
          note = tr('E-mail or password is wrong. Create the account in step 1 first if you have not.', 'E-mail ou senha incorretos. Se ainda não tem conta, crie uma na etapa 1.');
        }
        showResult($('#result-2'), res, note);
      });
    });

    $('#run-3').addEventListener('click', (event) => {
      busy(event.currentTarget, async () => {
        const session = loadSession();
        const res = await call('GET', '/api/v1/auth/me', undefined, true);
        let note = null;
        if (res.ok) {
          markDone('stop-3');
          note = tr('The server read your id, e-mail and role from the token. It did not look anything up.', 'O servidor leu seu id, e-mail e perfil direto do token. Não consultou o banco.');
        } else if (!session) {
          note = tr('No token yet. Complete step 1 or 2, then run this again.', 'Ainda não há token. Faça a etapa 1 ou 2 e rode de novo.');
        } else if (res.status === 401) {
          note = tr('The access token was rejected, most likely because it expired. Renew it in step 4.', 'O access token foi recusado, provavelmente porque expirou. Renove-o na etapa 4.');
        }
        showResult($('#result-3'), res, note);
      });
    });

    $('#run-4').addEventListener('click', (event) => {
      busy(event.currentTarget, async () => {
        const session = loadSession();
        if (!session || !session.refreshToken) {
          showResult($('#result-4'),
            { ok: false, status: 0, statusText: tr('No session', 'Sem sessão'), data: null, ms: 0 },
            tr('There is no refresh token yet. Complete step 1 or 2 first.', 'Ainda não há refresh token. Faça a etapa 1 ou 2 primeiro.'));
          return;
        }
        const res = await call('POST', '/api/v1/auth/refresh', { refreshToken: session.refreshToken }, false);
        let note = null;
        if (res.ok) {
          saveSession(sessionFromAuth(res.data, session.refreshToken));
          markDone('stop-4');
          note = tr('You have a new pair. The previous refresh token is now dead. Try replaying it with the other button.', 'Você tem um novo par. O refresh token anterior deixou de funcionar. Tente reenviá-lo com o outro botão.');
        } else if (res.status === 401) {
          note = tr('This refresh token was rejected. Sign in again in step 2.', 'Esse refresh token foi recusado. Entre de novo na etapa 2.');
        }
        showResult($('#result-4'), res, note);
      });
    });

    $('#run-4b').addEventListener('click', (event) => {
      busy(event.currentTarget, async () => {
        const session = loadSession();
        if (!session || !session.previousRefresh) {
          showResult($('#result-4'),
            { ok: false, status: 0, statusText: tr('Nothing to replay', 'Nada para reenviar'), data: null, ms: 0 },
            tr('Run "Refresh tokens" first. That leaves an old token you can replay.', 'Rode "Renovar tokens" primeiro. Isso deixa um token antigo para reenviar.'));
          return;
        }
        const res = await call('POST', '/api/v1/auth/refresh', { refreshToken: session.previousRefresh }, false);
        const note = res.status === 401
          ? tr('Rejected, as expected. The server also revoked every refresh token for this account, so the newest one no longer works either. Sign in again in step 2. Access tokens stay valid until they expire.', 'Recusado, como esperado. O servidor também revogou todos os refresh tokens da conta, então o mais novo também deixou de funcionar. Entre de novo na etapa 2. Os access tokens continuam válidos até expirar.')
          : null;
        showResult($('#result-4'), res, note);
      });
    });

    $('#clear-session').addEventListener('click', () => {
      saveSession(null);
      $$('.stop').forEach((stop) => stop.classList.remove('done'));
    });

    $$('[data-copy-target]').forEach((button) => {
      button.addEventListener('click', async () => {
        const source = document.getElementById(button.dataset.copyTarget);
        if (!source) return;
        const original = button.textContent;
        try {
          await navigator.clipboard.writeText(source.textContent);
          button.textContent = tr('Copied', 'Copiado');
        } catch {
          button.textContent = tr('Select and copy', 'Selecione e copie');
        }
        setTimeout(() => { button.textContent = original; }, 1600);
      });
    });

    $('#base-url').textContent = `${location.origin}/api/v1`;
  }

  /* ---------- Status and navigation ---------- */

  async function checkHealth() {
    const status = $('#api-status');
    const text = $('#api-status-text');
    try {
      const response = await fetch('/health', { cache: 'no-store' });
      if (!response.ok) throw new Error('unhealthy');
      status.className = 'status ok';
      text.textContent = tr('API is online', 'API online');
    } catch {
      status.className = 'status down';
      text.textContent = tr('API is not responding', 'API não responde');
    }
  }

  function watchSections() {
    const links = new Map($$('.rail-nav a').map((a) => [a.getAttribute('href').slice(1), a]));
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        links.forEach((link) => link.classList.remove('active'));
        const link = links.get(entry.target.id);
        if (link) link.classList.add('active');
      }
    }, { rootMargin: '-30% 0px -60% 0px' });
    links.forEach((_, id) => { const section = document.getElementById(id); if (section) observer.observe(section); });
  }

  /* ---------- OpenAPI rendering ---------- */

  let schemas = {};

  function deref(schema) {
    if (schema && schema.$ref) {
      const name = schema.$ref.split('/').pop();
      return { name, schema: schemas[name] || {} };
    }
    return { name: null, schema: schema || {} };
  }

  function typeLabel(input) {
    const { name, schema } = deref(input);
    if (name) return name;
    if (schema.enum) return schema.enum.join(' | ');
    if (schema.type === 'array') return `${typeLabel(schema.items)}[]`;
    const type = Array.isArray(schema.type) ? schema.type.filter((t) => t !== 'null').join(' | ') : schema.type;
    if (!type) return 'object';
    return schema.format ? `${type} (${schema.format})` : type;
  }

  function exampleValue(input, key = '', depth = 0) {
    const session = loadSession();
    const known = {
      fullName: 'Maria Souza',
      email: session && session.user ? session.user.email : 'driver@example.com',
      phoneNumber: '+5511999999999',
      password: 'Str0ngPass1',
      role: 'Driver',
      refreshToken: session && session.refreshToken ? session.refreshToken : '<refresh token>'
    };
    if (Object.prototype.hasOwnProperty.call(known, key)) return known[key];

    const { schema } = deref(input);
    if (depth > 4) return null;
    if (schema.enum) return schema.enum[0];
    if (schema.type === 'object' || schema.properties) {
      const result = {};
      for (const [name, property] of Object.entries(schema.properties || {})) {
        result[name] = exampleValue(property, name, depth + 1);
      }
      return result;
    }
    if (schema.type === 'array') return [exampleValue(schema.items, key, depth + 1)];
    if (schema.type === 'integer' || schema.type === 'number') return 0;
    if (schema.type === 'boolean') return false;
    if (schema.format === 'uuid') return '00000000-0000-0000-0000-000000000000';
    if (schema.format === 'date-time') return new Date().toISOString();
    return 'string';
  }

  function fieldTable(input, withNotes) {
    const { schema } = deref(input);
    const required = schema.required || [];
    const rows = Object.entries(schema.properties || {});
    if (!rows.length) return h('p', { class: 'muted' }, tr('No fields.', 'Sem campos.'));
    return h('div', { class: 'table-wrap' },
      h('table', { class: 'grid' },
        h('thead', {}, h('tr', {},
          h('th', {}, tr('Field', 'Campo')), h('th', {}, tr('Type', 'Tipo')), withNotes ? h('th', {}, tr('Rules', 'Regras')) : null)),
        h('tbody', {}, rows.map(([name, property]) => h('tr', {},
          h('td', {}, h('code', {}, name), required.includes(name) ? h('span', { class: 'req', title: tr('Required', 'Obrigatório') }, ' *') : null),
          h('td', {}, typeLabel(property)),
          withNotes ? h('td', {}, FIELD_NOTES[name] || '') : null)))));
  }

  function jsonSchemaOf(content) {
    return content && content['application/json'] ? content['application/json'].schema : null;
  }

  function buildOperation(path, method, operation) {
    const secured = Array.isArray(operation.security) && operation.security.length > 0;
    const bodySchema = operation.requestBody ? jsonSchemaOf(operation.requestBody.content) : null;
    const parameters = (operation.parameters || []).filter((p) => p.in === 'path' || p.in === 'query');
    const paramInputs = {};
    let textarea = null;

    const successEntry = Object.entries(operation.responses || {})
      .find(([code, response]) => code.startsWith('2') && jsonSchemaOf(response.content));

    const result = h('div', { class: 'result', hidden: true });
    const sendButton = h('button', { type: 'button', class: 'btn' }, tr('Send request', 'Enviar requisição'));

    if (bodySchema) {
      textarea = h('textarea', { 'aria-label': tr(`Request body for ${method.toUpperCase()} ${path}`, `Corpo da requisição de ${method.toUpperCase()} ${path}`), spellcheck: 'false' });
      textarea.value = JSON.stringify(exampleValue(bodySchema), null, 2);
    }

    sendButton.addEventListener('click', () => busy(sendButton, async () => {
      let url = path;
      const query = new URLSearchParams();
      for (const parameter of parameters) {
        const value = paramInputs[parameter.name].value.trim();
        if (parameter.in === 'path') url = url.replace(`{${parameter.name}}`, encodeURIComponent(value));
        else if (value) query.set(parameter.name, value);
      }
      if ([...query].length) url += `?${query}`;

      let body;
      if (textarea) {
        try { body = JSON.parse(textarea.value); } catch (error) {
          showResult(result, { ok: false, status: 0, statusText: tr('Invalid JSON', 'JSON inválido'), data: null, ms: 0 },
            tr(`The request body is not valid JSON: ${error.message}`, `O corpo da requisição não é um JSON válido: ${error.message}`));
          return;
        }
      }

      const session = loadSession();
      const res = await call(method.toUpperCase(), url, body, secured);
      const note = secured && !session
        ? tr('This endpoint needs a token and you are not signed in. Complete step 1 or 2 in the guide.', 'Este endpoint exige token e você não está conectado. Faça a etapa 1 ou 2 do guia.')
        : null;
      showResult(result, res, note);
    }));

    return h('details', { class: 'op' },
      h('summary', {},
        h('span', { class: `method ${method}` }, method.toUpperCase()),
        h('span', { class: 'op-main' },
          h('span', { class: 'op-path' }, path), h('br'),
          h('span', { class: 'op-summary' }, operation.summary || '')),
        h('span', { class: `lock${secured ? '' : ' open'}` }, secured ? tr('Needs token', 'Exige token') : tr('Public', 'Público'))),
      h('div', { class: 'op-body' },
        operation.description ? h('p', { class: 'op-desc' }, operation.description) : null,
        parameters.length ? [h('h4', {}, tr('Parameters', 'Parâmetros')),
          h('div', { class: 'form-grid' }, parameters.map((parameter) => {
            const input = h('input', { name: parameter.name, required: !!parameter.required });
            paramInputs[parameter.name] = input;
            return h('label', {}, `${parameter.name} (${parameter.in})`, input);
          }))] : null,
        bodySchema ? [h('h4', {}, tr('Request body', 'Corpo da requisição')), fieldTable(bodySchema, true)] : null,
        successEntry ? [h('h4', {}, tr(`Returns (${successEntry[0]})`, `Retorna (${successEntry[0]})`)), fieldTable(jsonSchemaOf(successEntry[1].content), false)] : null,
        h('h4', {}, tr('Responses', 'Respostas')),
        h('ul', { class: 'resp-list' }, Object.entries(operation.responses || {}).map(([code, response]) => {
          const schema = jsonSchemaOf(response.content);
          const label = response.description || STATUS_TEXT[code] || '';
          const named = schema && schema.$ref ? typeLabel(schema) : null;
          return h('li', {},
            h('span', { class: `resp-code${code.startsWith('2') ? '' : ' bad'}` }, code),
            h('span', {}, named ? tr(`${label}. Returns ${named}.`, `${label}. Retorna ${named}.`) : label));
        })),
        h('h4', {}, 'Try it'),
        h('div', { class: 'try' },
          textarea,
          h('div', { class: 'actions', style: 'margin-top:10px' }, sendButton),
          result)));
  }

  async function loadEndpoints() {
    const list = $('#endpoint-list');
    let doc;
    try {
      const response = await fetch('/openapi/v1.json', { cache: 'no-store' });
      if (!response.ok) throw new Error(String(response.status));
      doc = await response.json();
    } catch {
      list.replaceChildren(h('p', {}, tr('The endpoint list could not be loaded from /openapi/v1.json. Make sure the API runs in the Development environment, then reload.', 'Não foi possível carregar a lista de /openapi/v1.json. Confirme que a API roda em Development e recarregue.')));
      return;
    }

    schemas = (doc.components && doc.components.schemas) || {};
    const groups = new Map();
    for (const [path, item] of Object.entries(doc.paths || {})) {
      for (const [method, operation] of Object.entries(item)) {
        if (!['get', 'post', 'put', 'patch', 'delete'].includes(method)) continue;
        const tag = (operation.tags && operation.tags[0]) || 'Endpoints';
        if (!groups.has(tag)) groups.set(tag, []);
        groups.get(tag).push({ path, method, operation });
      }
    }

    if (!groups.size) {
      list.replaceChildren(h('p', { class: 'muted' }, tr('No endpoints found.', 'Nenhum endpoint encontrado.')));
      return;
    }

    list.replaceChildren(...Array.from(groups.entries()).flatMap(([tag, operations]) => [
      h('h3', { class: 'group-title' }, tag),
      ...operations.map(({ path, method, operation }) => buildOperation(path, method, operation))
    ]));
  }

  /* ---------- Boot ---------- */

  setupGuide();
  renderSession();
  checkHealth();
  watchSections();
  loadEndpoints();
  setInterval(renderSession, 30000);
})();
