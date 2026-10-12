/* Segmented documentation: one page per journey, each step with a "Let's try" panel.
 * The user edits route parameters, query string and JSON body before sending. Values returned by one call
 * (ids, tokens) are kept and prefill the next steps. Content lives in journeys.data.js. */
(() => {
  'use strict';

  const JOURNEYS = window.FROTA_JOURNEYS || [];
  const API = '/api/v1';
  const STORE_KEY = 'frota.journeys.v1';
  const ROLES = ['Customer', 'Driver', 'Admin'];
  const ROLE_LABEL = { Customer: 'Cliente', Driver: 'Motorista', Admin: 'Admin', Any: 'Sessão em uso' };
  const STATUS_TEXT = {
    200: 'OK', 201: 'Created', 204: 'No Content', 400: 'Bad Request', 401: 'Unauthorized',
    403: 'Forbidden', 404: 'Not Found', 409: 'Conflict', 500: 'Server Error'
  };
  const BUILTIN = new Set(['rand', 'plate', 'cpf', 'cnh', 'refreshToken']);
  const ADMIN_LOGIN = { email: 'admin@frotaonline.local', password: 'Admin#12345' };

  const $ = (selector, root = document) => root.querySelector(selector);

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

  /* ------------------------------------------------------------------ state */

  let state = { sessions: {}, active: 'Customer', vars: {} };
  try {
    const raw = localStorage.getItem(STORE_KEY);
    if (raw) state = Object.assign(state, JSON.parse(raw));
  } catch { /* storage unavailable: the in-memory copy still works for this page */ }

  const listeners = [];
  function save() {
    try { localStorage.setItem(STORE_KEY, JSON.stringify(state)); } catch { /* ignore */ }
    listeners.forEach((fn) => fn());
    renderSessions();
    renderSaved();
  }

  const pick = (chars) => chars[Math.floor(Math.random() * chars.length)];
  const digits = (n) => Array.from({ length: n }, () => pick('0123456789')).join('');

  function randomCpf() {
    for (;;) {
      const base = Array.from({ length: 9 }, () => Math.floor(Math.random() * 10));
      if (base.every((d) => d === base[0])) continue;
      const check = (list) => {
        const sum = list.reduce((acc, d, i) => acc + d * (list.length + 1 - i), 0);
        const rest = (sum * 10) % 11;
        return rest === 10 ? 0 : rest;
      };
      const d1 = check(base);
      const d2 = check([...base, d1]);
      return [...base, d1, d2].join('');
    }
  }

  const GENERATORS = {
    rand: () => digits(6),
    plate: () => `${pick('ABCDEFGHJKLMNPRSTUVWXYZ')}${pick('ABCDEFGHJKLMNPRSTUVWXYZ')}${pick('ABCDEFGHJKLMNPRSTUVWXYZ')}${digits(1)}${pick('ABCDEFGHJKLMNPRSTUVWXYZ')}${digits(2)}`,
    cpf: randomCpf,
    cnh: () => digits(11)
  };

  function sessionFor(role) {
    const key = role === 'Any' ? state.active : role;
    return state.sessions[key] || null;
  }

  function getVar(name, role) {
    if (name === 'refreshToken') {
      const session = sessionFor(role || 'Any');
      return session ? session.refreshToken : undefined;
    }
    if (state.vars[name] === undefined && GENERATORS[name]) {
      state.vars[name] = GENERATORS[name]();
      try { localStorage.setItem(STORE_KEY, JSON.stringify(state)); } catch { /* ignore */ }
    }
    return state.vars[name];
  }

  /** "{{x}}" alone keeps the saved value as it is (numbers stay numbers); inside a longer text it is replaced as text. */
  function resolve(value, role) {
    if (typeof value === 'string') {
      const whole = value.match(/^\{\{(\w+)\}\}$/);
      if (whole) {
        const found = getVar(whole[1], role);
        return found === undefined ? '' : found;
      }
      return value.replace(/\{\{(\w+)\}\}/g, (_, name) => {
        const found = getVar(name, role);
        if (found === undefined) return '';
        return typeof found === 'object' ? JSON.stringify(found) : String(found);
      });
    }
    if (Array.isArray(value)) return value.map((item) => resolve(item, role));
    if (value && typeof value === 'object') {
      return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, resolve(item, role)]));
    }
    return value;
  }

  function placeholders(value, found = new Set()) {
    if (typeof value === 'string') {
      for (const match of value.matchAll(/\{\{(\w+)\}\}/g)) found.add(match[1]);
    } else if (Array.isArray(value)) value.forEach((item) => placeholders(item, found));
    else if (value && typeof value === 'object') Object.values(value).forEach((item) => placeholders(item, found));
    return found;
  }

  /** Where each saved value comes from, to tell the reader which step fills an empty field. */
  const PRODUCERS = {};
  for (const journey of JOURNEYS) {
    for (const flow of journey.flows) {
      for (const step of flow.steps) {
        for (const name of Object.keys(step.capture || {})) {
          if (!PRODUCERS[name]) PRODUCERS[name] = { journey, step };
        }
      }
    }
  }

  function readPath(source, path) {
    let current = source;
    for (const token of path.match(/[^.[\]]+/g) || []) {
      if (current == null) return undefined;
      current = current[token];
    }
    return current;
  }

  /* ------------------------------------------------------------------ HTTP */

  async function rawCall(method, url, body, token) {
    const headers = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (token) headers.Authorization = `Bearer ${token}`;
    const started = performance.now();
    try {
      const response = await fetch(url, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined });
      const text = await response.text();
      let data = text;
      try { data = text ? JSON.parse(text) : null; } catch { /* keep the raw text */ }
      return {
        ok: response.ok, status: response.status,
        statusText: response.statusText || STATUS_TEXT[response.status] || '',
        data, ms: Math.round(performance.now() - started)
      };
    } catch {
      return {
        ok: false, status: 0, statusText: 'Sem resposta', ms: Math.round(performance.now() - started),
        data: { detail: 'Não foi possível acessar o servidor. Confira se a API está rodando e tente de novo.' }
      };
    }
  }

  function storeAuth(auth) {
    const role = auth.user.role;
    state.sessions[role] = {
      accessToken: auth.accessToken,
      accessTokenExpiresAt: auth.accessTokenExpiresAt,
      refreshToken: auth.refreshToken,
      user: auth.user
    };
    state.active = role;
    save();
  }

  /** Renews the token shortly before it expires, so a long reading session does not end in a 401. */
  async function freshToken(role) {
    const key = role === 'Any' ? state.active : role;
    const session = state.sessions[key];
    if (!session) return null;
    const left = new Date(session.accessTokenExpiresAt).getTime() - Date.now();
    if (left < 20000 && session.refreshToken) {
      const res = await rawCall('POST', `${API}/auth/refresh`, { refreshToken: session.refreshToken });
      if (res.ok && res.data && res.data.accessToken) {
        storeAuth(res.data);
        return res.data.accessToken;
      }
    }
    return session.accessToken;
  }

  /* ------------------------------------------------------------------ session bar */

  function renderSessions() {
    const box = $('#session-rows');
    if (!box) return;
    box.replaceChildren(...ROLES.map((role) => {
      const session = state.sessions[role];
      const row = h('div', { class: `srow${session ? ' live' : ''}${state.active === role ? ' active' : ''}` },
        h('span', { class: 'srow-name' }, h('span', { class: 'pip' }), ROLE_LABEL[role]),
        h('span', { class: 'srow-mail' }, session ? session.user.email : 'sem sessão'),
        h('span', { class: 'srow-actions' },
          session && state.active !== role
            ? h('button', { type: 'button', class: 'chip-btn', onclick: () => { state.active = role; save(); } }, 'Usar')
            : null,
          !session
            ? h('button', { type: 'button', class: 'chip-btn', onclick: (event) => quickSignIn(role, event.currentTarget) },
              role === 'Admin' ? 'Entrar (seed)' : 'Conta de teste')
            : null,
          session
            ? h('button', { type: 'button', class: 'chip-btn', onclick: () => { delete state.sessions[role]; save(); } }, 'Sair')
            : null));
      return row;
    }));
  }

  function setMessage(text) {
    const message = $('#session-msg');
    if (message) message.textContent = text || '';
  }

  /** One click: creates a throwaway account, signs in and accepts the pending terms. Admin signs in with the seed account. */
  async function quickSignIn(role, button) {
    button.disabled = true;
    setMessage('Criando a sessão…');
    try {
      let res;
      if (role === 'Admin') {
        res = await rawCall('POST', `${API}/auth/login`, ADMIN_LOGIN);
      } else {
        const id = digits(8);
        const body = {
          fullName: role === 'Driver' ? 'Motorista de Teste' : 'Cliente de Teste',
          email: `${role.toLowerCase()}+${id}@example.com`,
          phoneNumber: '+5511999999999',
          password: 'Str0ng#Pass',
          role
        };
        res = await rawCall('POST', `${API}/auth/register`, body);
        if (res.ok) {
          state.vars.lastEmail = body.email;
          state.vars.lastPassword = body.password;
        }
      }
      if (!res.ok || !res.data || !res.data.accessToken) {
        setMessage(role === 'Admin'
          ? 'Não foi possível entrar. O admin do seed só existe em Development (veja scripts/seed-admin.sql).'
          : `Não foi possível criar a conta (${res.status || 'sem resposta'}).`);
        return;
      }
      storeAuth(res.data);

      const pending = await rawCall('GET', `${API}/terms/pending`, undefined, res.data.accessToken);
      if (pending.ok && Array.isArray(pending.data)) {
        for (const term of pending.data) {
          await rawCall('POST', `${API}/terms/accept`, { termDocumentId: term.id }, res.data.accessToken);
        }
      }
      setMessage(`Pronto: ${ROLE_LABEL[role]} em uso.`);
    } finally {
      button.disabled = false;
    }
  }

  function renderSaved() {
    const list = $('#saved-list');
    if (!list) return;
    const entries = Object.entries(state.vars).filter(([, value]) => value !== '' && value != null);
    if (entries.length === 0) {
      list.replaceChildren(h('p', { class: 'session-sub' }, 'Nada salvo ainda. Os ids devolvidos pelas chamadas aparecem aqui.'));
      return;
    }
    const dl = h('dl', {});
    for (const [key, value] of entries) {
      const text = typeof value === 'object' ? JSON.stringify(value) : String(value);
      dl.append(h('dt', {}, key), h('dd', { title: text }, text));
    }
    list.replaceChildren(dl);
  }

  /* ------------------------------------------------------------------ step */

  function pathParams(path) {
    return Array.from(path.matchAll(/\{(\w+)\}/g), (match) => match[1]);
  }

  function buildCurl(method, url, body, secured) {
    const parts = [`curl -X ${method} '${location.origin}${url}'`];
    if (secured) parts.push(`-H 'Authorization: Bearer $TOKEN'`);
    if (body !== undefined) {
      parts.push(`-H 'Content-Type: application/json'`);
      parts.push(`-d '${JSON.stringify(body).replace(/'/g, `'\\''`)}'`);
    }
    return parts.join(' \\\n  ');
  }

  function showResult(container, res) {
    container.hidden = false;
    container.className = `result ${res.ok ? 'ok' : 'fail'}`;
    container.replaceChildren(
      h('div', { class: 'result-head' },
        h('span', {}, res.status ? `${res.status} ${res.statusText}` : res.statusText),
        h('span', { class: 'ms' }, `${res.ms} ms`)),
      h('pre', {}, res.data == null || res.data === '' ? '(resposta vazia)'
        : typeof res.data === 'string' ? res.data : JSON.stringify(res.data, null, 2)));
  }

  function renderStep(step, number) {
    const role = step.role;
    const secured = role !== null && role !== undefined;
    const params = pathParams(step.path);
    const bind = step.bind || {};
    const isUpload = step.kind === 'upload';
    const fields = {};
    const dirty = new Set();

    const result = h('div', { class: 'result', hidden: true });
    const savedChips = h('div', { class: 'j-saved', hidden: true });
    const authLine = h('p', { class: 'j-auth' });
    const sendButton = h('button', { type: 'button', class: 'btn' }, isUpload ? 'Enviar arquivo' : 'Enviar requisição');
    const curlButton = h('button', { type: 'button', class: 'btn ghost' }, 'Copiar curl');
    const resetButton = h('button', { type: 'button', class: 'btn ghost' }, 'Restaurar exemplo');
    const bodyHint = h('span', { class: 'j-hint' });
    let textarea = null;
    let fileInput = null;

    const pathInputs = params.map((name) => {
      const hint = h('span', { class: 'j-hint' });
      const input = h('input', { name, autocomplete: 'off', spellcheck: 'false' });
      input.addEventListener('input', () => { dirty.add(`p:${name}`); updateHints(); });
      fields[`p:${name}`] = { input, hint, varName: bind[name] || name };
      return h('label', {}, `${name} (rota)`, input, hint);
    });

    const queryInputs = Object.keys(step.query || {}).map((name) => {
      const input = h('input', { name, autocomplete: 'off', spellcheck: 'false', placeholder: 'opcional' });
      input.addEventListener('input', () => dirty.add(`q:${name}`));
      fields[`q:${name}`] = { input, template: step.query[name] };
      return h('label', {}, `${name} (query)`, input);
    });

    if (step.body !== undefined) {
      textarea = h('textarea', { spellcheck: 'false', 'aria-label': `Corpo de ${step.method} ${step.path}` });
      textarea.addEventListener('input', () => { dirty.add('body'); validateBody(); });
    }
    if (isUpload) {
      fileInput = h('input', { type: 'file', accept: 'image/jpeg,image/png,application/pdf' });
      fileInput.addEventListener('change', () => {
        const file = fileInput.files[0];
        if (!file) return;
        state.vars.fileSize = file.size;
        state.vars.fileContentType = file.type || state.vars.fileContentType;
        save();
      });
    }

    function fillFields() {
      for (const [key, field] of Object.entries(fields)) {
        if (dirty.has(key)) continue;
        if (key.startsWith('p:')) {
          const value = getVar(field.varName, role);
          field.input.value = value === undefined ? '' : String(value);
        } else {
          field.input.value = String(resolve(field.template, role));
        }
      }
      if (textarea && !dirty.has('body')) textarea.value = JSON.stringify(resolve(step.body, role), null, 2);
      updateHints();
    }

    function updateHints() {
      for (const [key, field] of Object.entries(fields)) {
        if (!key.startsWith('p:')) continue;
        const missing = field.input.value.trim() === '';
        const origin = PRODUCERS[field.varName];
        field.hint.className = `j-hint${missing ? ' warn' : ''}`;
        field.hint.replaceChildren(...(missing && origin
          ? ['Vazio. Execute antes: ', h('a', { href: `#/${origin.journey.id}` }, origin.step.title)]
          : []));
      }
      if (textarea) {
        const names = [...placeholders(step.body)].filter((name) => !BUILTIN.has(name) && getVar(name, role) === undefined);
        const places = names.map((name) => (PRODUCERS[name] ? `${name} (passo "${PRODUCERS[name].step.title}")` : name));
        bodyHint.className = `j-hint${names.length ? ' warn' : ''}`;
        bodyHint.textContent = names.length ? `Faltam valores: ${places.join(', ')}.` : '';
      }
      const session = secured ? sessionFor(role) : null;
      if (!secured) {
        authLine.className = 'j-auth';
        authLine.textContent = isUpload ? 'Vai direto para a URL assinada, sem token.' : 'Endpoint público: não precisa de token.';
      } else if (session) {
        authLine.className = 'j-auth';
        authLine.textContent = `Usa a sessão: ${session.user.fullName} (${ROLE_LABEL[session.user.role]}), ${session.user.email}.`;
      } else {
        authLine.className = 'j-auth warn';
        authLine.textContent = role === 'Any'
          ? 'Nenhuma sessão ativa. Crie uma na barra "Sessões de teste".'
          : `Falta a sessão de ${ROLE_LABEL[role]}. Use o botão na barra "Sessões de teste".`;
      }
    }

    function validateBody() {
      if (!textarea) return true;
      try {
        JSON.parse(textarea.value);
        if (!bodyHint.classList.contains('warn')) bodyHint.textContent = '';
        return true;
      } catch (error) {
        bodyHint.className = 'j-hint warn';
        bodyHint.textContent = `JSON inválido: ${error.message}`;
        return false;
      }
    }

    function buildRequest() {
      let route = step.path;
      for (const name of params) {
        const value = fields[`p:${name}`].input.value.trim();
        if (!value) return { error: `Preencha o parâmetro ${name}.` };
        route = route.replace(`{${name}}`, encodeURIComponent(value));
      }
      const query = new URLSearchParams();
      for (const name of Object.keys(step.query || {})) {
        const value = fields[`q:${name}`].input.value.trim();
        if (value) query.set(name, value);
      }
      let url = `${API}${route}`;
      if ([...query].length) url += `?${query}`;
      let body;
      if (textarea) {
        try { body = JSON.parse(textarea.value); } catch (error) { return { error: `JSON inválido: ${error.message}` }; }
      }
      return { url, body };
    }

    function keepValues(res, requestBody) {
      const kept = [];
      for (const [name, path] of Object.entries(step.capture || {})) {
        const value = path.startsWith('req.') ? readPath(requestBody, path.slice(4)) : readPath(res.data, path);
        if (value !== undefined && value !== null && value !== '') {
          state.vars[name] = value;
          kept.push(name);
        }
      }
      if (step.auth && res.data && res.data.accessToken && res.data.user) {
        storeAuth(res.data);
        kept.push(`sessão ${ROLE_LABEL[res.data.user.role]}`);
      }
      if (kept.length) {
        save();
        savedChips.hidden = false;
        savedChips.replaceChildren(h('strong', {}, 'Salvo: '), ...kept.map((name) => h('span', {}, name)));
      } else {
        savedChips.hidden = true;
      }
    }

    let armed = null;
    async function run() {
      if (step.danger && !armed) {
        sendButton.textContent = 'Confirmar envio';
        armed = setTimeout(() => { armed = null; sendButton.textContent = 'Enviar requisição'; }, 4000);
        return;
      }
      if (armed) { clearTimeout(armed); armed = null; sendButton.textContent = isUpload ? 'Enviar arquivo' : 'Enviar requisição'; }

      sendButton.disabled = true;
      try {
        if (isUpload) {
          await runUpload();
          return;
        }
        const request = buildRequest();
        if (request.error) {
          showResult(result, { ok: false, status: 0, statusText: 'Faltam dados', data: { detail: request.error }, ms: 0 });
          return;
        }
        let token = null;
        if (secured) {
          token = await freshToken(role);
          if (!token) {
            showResult(result, {
              ok: false, status: 0, statusText: 'Sem sessão',
              data: { detail: `Não há sessão de ${ROLE_LABEL[role]}. Crie uma na barra "Sessões de teste" e envie de novo.` }, ms: 0
            });
            return;
          }
        }
        const res = await rawCall(step.method, request.url, request.body, token);
        showResult(result, res);
        if (res.ok) {
          keepValues(res, request.body);
          card.classList.add('done');
        } else {
          savedChips.hidden = true;
        }
      } finally {
        sendButton.disabled = false;
      }
    }

    async function runUpload() {
      const file = fileInput.files[0];
      const url = state.vars.uploadUrl;
      if (!url) {
        showResult(result, { ok: false, status: 0, statusText: 'Faltam dados', ms: 0, data: { detail: 'Peça a URL de envio no passo anterior.' } });
        return;
      }
      if (!file) {
        showResult(result, { ok: false, status: 0, statusText: 'Faltam dados', ms: 0, data: { detail: 'Escolha um arquivo.' } });
        return;
      }
      const started = performance.now();
      try {
        const response = await fetch(url, {
          method: state.vars.uploadMethod || 'PUT',
          headers: state.vars.uploadHeaders || {},
          body: file
        });
        showResult(result, {
          ok: response.ok, status: response.status, statusText: response.statusText || STATUS_TEXT[response.status] || '',
          ms: Math.round(performance.now() - started),
          data: response.ok ? { enviado: file.name, bytes: file.size } : await response.text()
        });
        if (response.ok) card.classList.add('done');
      } catch {
        showResult(result, {
          ok: false, status: 0, statusText: 'Sem resposta', ms: Math.round(performance.now() - started),
          data: { detail: 'O envio falhou. O bucket precisa permitir CORS para esta origem e a URL assinada pode ter expirado.' }
        });
      }
    }

    sendButton.addEventListener('click', run);
    curlButton.addEventListener('click', async () => {
      const request = buildRequest();
      const text = request.error ? request.error : buildCurl(step.method, request.url, request.body, secured);
      try {
        await navigator.clipboard.writeText(text);
        curlButton.textContent = 'Copiado';
      } catch {
        curlButton.textContent = 'Não foi possível copiar';
      }
      setTimeout(() => { curlButton.textContent = 'Copiar curl'; }, 1800);
    });
    resetButton.addEventListener('click', () => { dirty.clear(); fillFields(); validateBody(); });

    const card = h('article', { class: 'j-step', id: `step-${step.id}` },
      h('div', { class: 'j-head' },
        h('span', { class: `method ${step.method.toLowerCase()}` }, step.method),
        h('span', { class: 'j-path' }, isUpload ? '(uploadUrl devolvida)' : step.path),
        h('span', { class: `j-who${secured ? '' : ' pub'}` }, secured ? ROLE_LABEL[role] : 'Público')),
      h('h3', {}, `${number}. ${step.title}`),
      h('p', {}, step.desc),
      step.note ? h('p', { class: 'j-note' }, step.note) : null,
      h('details', { class: 'j-try' },
        h('summary', {}, "Let's try"),
        h('div', { class: 'j-try-body' },
          authLine,
          pathInputs.length ? h('div', {}, h('h4', {}, 'Parâmetros da rota'), h('div', { class: 'j-fields' }, pathInputs)) : null,
          queryInputs.length ? h('div', {}, h('h4', {}, 'Query string'), h('div', { class: 'j-fields' }, queryInputs)) : null,
          isUpload ? h('div', {}, h('h4', {}, 'Arquivo'), fileInput) : null,
          textarea ? h('div', {}, h('h4', {}, 'Corpo (JSON)'), textarea, bodyHint) : null,
          h('div', { class: 'j-actions' }, sendButton, isUpload ? null : curlButton, textarea || pathInputs.length || queryInputs.length ? resetButton : null),
          result,
          savedChips)));

    listeners.push(() => { if (card.isConnected) fillFields(); });
    fillFields();
    return card;
  }

  /* ------------------------------------------------------------------ pages */

  const stepCount = (journey) => journey.flows.reduce((sum, flow) => sum + flow.steps.length, 0);

  function renderNav(activeId) {
    const nav = $('#journey-nav');
    nav.replaceChildren(
      h('a', { href: '#/', class: activeId ? '' : 'active' }, 'Início'),
      h('p', { class: 'rail-group' }, 'Jornadas'),
      ...JOURNEYS.map((journey) => h('a', { href: `#/${journey.id}`, class: journey.id === activeId ? 'active' : '' },
        journey.title, h('span', { class: 'tag' }, journey.who))));
  }

  function renderHome() {
    return h('div', { class: 'j-page' },
      h('header', { class: 'j-hero' },
        h('h1', {}, 'Uma jornada de cada vez.'),
        h('p', {}, 'Cada jornada mostra só as chamadas daquele caminho, na ordem em que o app as faz. Em cada passo abra o "Let\'s try", mude os valores e envie a requisição de verdade.'),
        h('ol', { class: 'j-steps' },
          h('li', {}, 'Crie as sessões de teste na barra lateral'),
          h('li', {}, 'Escolha uma jornada'),
          h('li', {}, 'Siga os passos: os ids de um passo preenchem o próximo'))),
      h('div', { class: 'j-grid' }, JOURNEYS.map((journey) =>
        h('a', { class: 'j-card', href: `#/${journey.id}` },
          h('span', { class: 'who' }, journey.who),
          h('h2', {}, journey.title),
          h('p', {}, journey.summary),
          h('span', { class: 'count' }, `${journey.flows.length} ${journey.flows.length === 1 ? 'subfluxo' : 'subfluxos'}, ${stepCount(journey)} chamadas →`)))));
  }

  function renderJourney(journey) {
    const index = JOURNEYS.indexOf(journey);
    const previous = JOURNEYS[index - 1];
    const next = JOURNEYS[index + 1];
    let number = 0;

    return h('div', { class: 'j-page' },
      h('p', { class: 'j-crumb' }, h('a', { href: '#/' }, 'Jornadas'), ' / ', journey.who),
      h('h1', { class: 'j-title' }, journey.title),
      h('p', { class: 'j-sub' }, journey.summary),
      journey.flows.length > 1
        ? h('ul', { class: 'j-tabs' }, journey.flows.map((flow, i) =>
          h('li', {}, h('a', { href: `#/${journey.id}/${i + 1}` }, `${i + 1}. ${flow.title}`))))
        : null,
      journey.flows.map((flow, i) => h('section', { class: 'j-flow', id: `flow-${i + 1}` },
        h('h2', {}, h('span', { class: 'n' }, i + 1), flow.title),
        h('p', { class: 'j-intro' }, flow.intro),
        flow.steps.map((step) => renderStep(step, ++number)))),
      h('nav', { class: 'j-pager', 'aria-label': 'Outras jornadas' },
        previous ? h('a', { href: `#/${previous.id}` }, h('small', {}, '← Anterior'), previous.title) : h('span'),
        next ? h('a', { href: `#/${next.id}` }, h('small', {}, 'Próxima →'), next.title) : h('span')));
  }

  function route() {
    listeners.length = 0;
    const [id, flowIndex] = location.hash.replace(/^#\/?/, '').split('/');
    const journey = JOURNEYS.find((item) => item.id === id);
    const main = $('#content');
    main.replaceChildren(journey ? renderJourney(journey) : renderHome());
    renderNav(journey ? journey.id : null);
    document.title = journey ? `${journey.title} | Frota Online API` : 'Frota Online API: jornadas';

    const target = journey && flowIndex ? document.getElementById(`flow-${flowIndex}`) : null;
    if (target) target.scrollIntoView();
    else window.scrollTo(0, 0);
  }

  async function checkApi() {
    const status = $('#api-status');
    const text = $('#api-status-text');
    try {
      const response = await fetch('/health', { cache: 'no-store' });
      status.className = `status ${response.ok ? 'ok' : 'down'}`;
      text.textContent = response.ok ? 'API no ar' : 'API com problema';
    } catch {
      status.className = 'status down';
      text.textContent = 'API fora do ar';
    }
  }

  $('#clear-all').addEventListener('click', () => {
    state = { sessions: {}, active: 'Customer', vars: {} };
    save();
    setMessage('Sessões e valores salvos apagados.');
  });

  window.addEventListener('hashchange', route);
  renderSessions();
  renderSaved();
  route();
  checkApi();
})();
