---
name: kubernetes-plan
description: Kubernetes-розгортання всього стека — ЗРОБЛЕНО й live-verified ([TL-62]); маніфести в k8s/ + kustomization.yaml у корені
metadata:
  node_type: memory
  type: project
---

**СТАН: ЗРОБЛЕНО (2026-07-04, [TL-62]).** Весь стек із `docker-compose.yml` перекладено в Kubernetes і **перевірено на живому кластері** (Docker Desktop k8s, node Ready v1.32.2). Раніше кластера не було — цю сесію юзер увімкнув Docker Desktop → Kubernetes.

**Де що лежить:**
- `kustomization.yaml` — **у корені репо** (не в k8s/), бо `configMapGenerator` тягне `monitoring/*` у ConfigMap-и, а kustomize load-restrictor не дозволяє посилатись на файли вище каталогу kustomization.
- `k8s/*.yaml` — 17 маніфестів (namespace, secret, config, mongo, redis, rabbitmq, jaeger, 5 сервісів, gateway, web, prometheus, alertmanager, grafana) + `k8s/README.md`.
- Команда: `docker compose build` → `kubectl apply -k .`. Тір-даун: `kubectl delete -k .`.

**Ключові рішення (усі спрацювали):**
- **Імена k8s Service = імена compose-сервісів** (`identity`, `gateway`, `prometheus`, `mongodb` тощо) → `monitoring/prometheus.yml`, YARP-destinations гейтвею й web `Gateway__BaseUrl` переносяться БЕЗ ЗМІН.
- **Образи локальні** `telegramlike-*:latest`, `imagePullPolicy: IfNotPresent` (Docker Desktop k8s ділить docker-демон — образи видно, нічого не тягнеться з registry).
- **Mongo rs0**: `StatefulSet` (1 репліка) + PVC + окремий **Job `mongo-rs-init`**, що чекає mongod і робить ідемпотентний `rs.initiate` (member host `localhost:27017`, як у compose). Клієнти — `directConnection=true`. Job відпрацював: "replica set initiated".
- **Спільний `JwtSecret`** → k8s `Secret` `app-secrets`; спільний env → ConfigMap `app-config` (envFrom на всіх 5 сервісах); per-service `MongoDB__DatabaseName` (+ Redis для identity/presence) — inline env.
- **Web**: NodePort **30080** (http://localhost:30080) + PVC для DataProtection keys (`/var/dp-keys`). Web не має /health → readiness = TCP-probe :8080. Решта — httpGet `/health/ready`.
- **Моніторинг configs** (prometheus.yml, rules.yml, alertmanager.yml, grafana provisioning + dashboard JSON) → ConfigMap-и через `configMapGenerator` з `disableNameSuffixHash: true`.

**Live-verify (усе green):**
- Усі 14 компонентів `1/1 Running`; Job `Completed`. notifications рестартнувся 2 рази поки Mongo/RabbitMQ піднімались, тоді сів — очікувана churn (сервіси не чекають Job'а).
- Smoke через gateway (port-forward :8090): register 200 → login → token-exchange (identity підписав JWT) → **create group chat 201** (chats валідував JWT + Mongo-транзакція) → list my chats 200 повертає чат. Шлях до chats через gateway подвоєний: `/chats/chats/group`.
- Prometheus: **8/8 targets up**. Web NodePort → 302 (Blazor редіректить на login = працює).

**Каверзи на майбутнє:** сервіси не мають `depends_on`-семантики k8s — вони крешлуплять поки Mongo не стане replica set і RabbitMQ не підніметься, тоді відновлюються (readiness failureThreshold 6). Можна додати initContainer-wait, але для pet-кластера не варто. Kustomize у корені — не плутати з `docker-compose.yml`, обидва валідні деплої.

Див. [[observability-metrics]], [[api-gateway]], [[microservices-migration]], [[telegramlike-project-status]], [[bff-resilience]].
