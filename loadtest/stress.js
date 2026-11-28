// Stresstest: hvor mange lobby-indlæsninger i sekundet kan én instans klare?
// Kør mod testbackenden med serverens grænser (docker-compose.loadtest.yml).
//   k6 run loadtest/stress.js
// Én iteration = én lobby-indlæsning (games, map, rules). Raten stiger i trin, indtil
// p95 passerer 1 s eller over 5 % fejler. Så stopper testen af sig selv.
import http from 'k6/http'
import { check } from 'k6'

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5081'
const USERS = 20

// Hvert trin: 20 s op til raten, 40 s ved raten. Tallene er lobby-indlæsninger pr. sekund (3 requests hver).
const RATES = [10, 25, 50, 75, 100, 150, 200, 300]
const stages = RATES.flatMap((rate) => [
  { duration: '20s', target: rate },
  { duration: '40s', target: rate },
])

export const options = {
  setupTimeout: '3m',
  scenarios: {
    stress: {
      executor: 'ramping-arrival-rate',
      startRate: 5,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 500,
      stages,
    },
  },
  thresholds: {
    'http_req_duration{name:games}': [{ threshold: 'p(95)<1000', abortOnFail: true, delayAbortEval: '20s' }],
    'http_req_failed{scenario:stress}': [{ threshold: 'rate<0.05', abortOnFail: true, delayAbortEval: '20s' }],
    'http_req_duration{name:map}': ['p(95)<1000'],
    'http_req_duration{name:rules}': ['p(95)<1000'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
}

// Kører én gang før testen. Registreringerne tæller ikke med i scenariets tal.
export function setup() {
  const tokens = []
  for (let i = 0; i < USERS; i++) {
    const id = `stress-${i}-${Date.now()}`
    const res = http.post(
      `${BASE_URL}/api/auth/register`,
      JSON.stringify({
        userName: `k6 ${id}`,
        email: `k6-${id}@example.test`,
        password: 'belastningstest-kodeord-2026',
      }),
      { headers: { 'Content-Type': 'application/json' }, tags: { name: 'register' } },
    )
    if (res.status !== 200) throw new Error(`register gav ${res.status}: ${res.body}`)
    tokens.push(res.json('accessToken'))
  }
  return { tokens }
}

export default function (data) {
  const token = data.tokens[__VU % data.tokens.length]
  const params = (name) => ({ headers: { Authorization: `Bearer ${token}` }, tags: { name } })

  check(http.get(`${BASE_URL}/api/games`, params('games')), { 'games 200': (r) => r.status === 200 })
  check(http.get(`${BASE_URL}/api/map`, params('map')), { 'map 200': (r) => r.status === 200 })
  check(http.get(`${BASE_URL}/api/rules/card-trades`, params('rules')), {
    'rules 200': (r) => r.status === 200,
  })
}