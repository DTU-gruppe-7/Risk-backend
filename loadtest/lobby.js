// Belastningstest af lobbyens kald. Kør mod den lokale testbackend (docker-compose.loadtest.yml):
//   k6 run loadtest/lobby.js
// Hver virtuel bruger registrerer sig én gang og genbruger tokenet. Registreringen er tagget
// for sig og indgår ikke i tærsklerne, fordi PBKDF2 med 210.000 iterationer er bevidst langsom.
import http from 'k6/http'
import { check, fail, sleep } from 'k6'

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5081'
const JSON_HEADERS = { 'Content-Type': 'application/json' }

export const options = {
  scenarios: {
    lobby: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 10 },
        { duration: '40s', target: 10 },
        { duration: '20s', target: 25 },
        { duration: '40s', target: 25 },
        { duration: '20s', target: 50 },
        { duration: '60s', target: 50 },
        { duration: '20s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    // Referenceværdier, ikke krav fra en kunde. Testen fejler, hvis de overskrides.
    'http_req_failed{name:games}': ['rate<0.01'],
    'http_req_duration{name:games}': ['p(95)<500'],
    'http_req_duration{name:map}': ['p(95)<500'],
    'http_req_duration{name:rules}': ['p(95)<500'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
}

// Én variabel pr. virtuel bruger: hver VU kører sin egen kopi af modulet.
let token = null

function register() {
  const id = `${__VU}-${Date.now()}`
  const res = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({
      userName: `k6 ${id}`,
      email: `k6-${id}@example.test`,
      password: 'belastningstest-kodeord-2026',
    }),
    { headers: JSON_HEADERS, tags: { name: 'register' } },
  )
  if (!check(res, { 'register 200': (r) => r.status === 200 })) {
    fail(`register gav ${res.status}: ${res.body}`)
  }
  return res.json('accessToken')
}

export default function () {
  if (!token) token = register()

  const params = (name) => ({ headers: { Authorization: `Bearer ${token}` }, tags: { name } })

  check(http.get(`${BASE_URL}/api/games`, params('games')), { 'games 200': (r) => r.status === 200 })
  check(http.get(`${BASE_URL}/api/map`, params('map')), { 'map 200': (r) => r.status === 200 })
  check(http.get(`${BASE_URL}/api/rules/card-trades`, params('rules')), {
    'rules 200': (r) => r.status === 200,
  })

  sleep(1)
}