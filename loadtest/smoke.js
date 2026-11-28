// Røgtest: én bruger, én gang. Tjekker at kæden virker, før vi lægger belastning på.
//   k6 run loadtest/smoke.js
import http from 'k6/http'
import { check, fail } from 'k6'

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5081'
const JSON_HEADERS = { 'Content-Type': 'application/json' }

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: { checks: ['rate==1'] },
}

export default function () {
  const id = `${__VU}-${Date.now()}`
  const register = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({
      userName: `k6 ${id}`,
      email: `k6-${id}@example.test`,
      password: 'belastningstest-kodeord-2026',
    }),
    { headers: JSON_HEADERS },
  )
  if (!check(register, { 'register 200': (r) => r.status === 200 })) {
    fail(`register gav ${register.status}: ${register.body}`)
  }

  const auth = { headers: { Authorization: `Bearer ${register.json('accessToken')}` } }

  check(http.get(`${BASE_URL}/api/map`, auth), { 'map 200': (r) => r.status === 200 })
  check(http.get(`${BASE_URL}/api/games`, auth), { 'games 200': (r) => r.status === 200 })
  check(http.get(`${BASE_URL}/api/rules/card-trades`, auth), {
    'rules 200': (r) => r.status === 200,
  })
}