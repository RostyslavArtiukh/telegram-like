---
name: kubernetes-plan
description: Наступна велика задача — розгортання в Kubernetes; план і стан машини (робота почнеться у свіжій сесії)
metadata:
  node_type: memory
  type: project
---

**Рішення (2026-07-04):** наступна велика задача — перекласти весь стек із `docker-compose` у **Kubernetes**. Домовились почати її у **новій сесії** (ця вже дуже довга). Поточний деплой — лише docker-compose; жодних k8s-маніфестів у репо ще немає.

**Стан машини (перевірено):**
- `kubectl` v1.32.2 встановлено, але **робочого кластера НЕМАЄ** (немає активного context; `kubectl get nodes` не відповідає). Перший крок нової сесії: попросити юзера увімкнути **Docker Desktop → Settings → Kubernetes → Enable** (або kind/minikube).
- **Helm НЕ встановлено** → використовувати **звичайні маніфести** або **Kustomize** (вбудований у kubectl, `kubectl apply -k`). Не тягнути Helm без потреби.
- Live-перевірка (pod'и піднялись, застосунок відкривається) потрібна перед тим, як казати «готово» — тому кластер обовʼязковий.

**Що перекладати (14 компонентів з `docker-compose.yml`):** 5 сервісів (identity 8085 / notifications 8081 / presence 8082 / chats 8083 / messaging 8084) + gateway 8090 + web 8080 + mongo + redis + rabbitmq + jaeger + prometheus 9090 + grafana 3000 + alertmanager 9093. Усі app-контейнери слухають :8080 всередині.

**Ключові каверзи:**
- **Образи збираються локально** (`telegramlike-*:latest`). Docker Desktop k8s ділить docker-демон, тож образи доступні — став `imagePullPolicy: IfNotPresent` (або Never), інакше спробує тягнути з registry й впаде.
- **Mongo — single-node replica set `rs0`** (потрібен для транзакцій). У compose ініціюється healthcheck-ом (`rs.initiate`). У k8s: StatefulSet + окремий init (Job/postStart/initContainer, що робить `rs.initiate`). Це найскладніша частина.
- **Спільний `ServiceAuth:JwtSecret`** у всіх сервісах → один k8s `Secret`, монтований у всі. (Значення в compose: `2VfJYDFD...`.)
- **Per-service Mongo DB** — один інстанс Mongo, різні імена БД (`telegramlike_identity` тощо) через env `MongoDB__DatabaseName`.
- **Конфіги монітирингу** (`monitoring/prometheus.yml`, `rules.yml`, `grafana/provisioning/**`, `alertmanager/alertmanager.yml`) → `ConfigMap`-и, монтовані у відповідні pod'и.
- **Вхід ззовні**: web (і, можливо, gateway/grafana) через `Ingress` (Docker Desktop має ingress-nginx? — перевірити) або `NodePort`/`port-forward`.
- Env-конфіг сервісів (`__` роздільник) → ConfigMap/Secret; RabbitMQ vhost `telegramlike`; Jaeger OTLP `http://jaeger:4317`; DataProtection keys для web → PVC.

**Пропонований фазовий план:**
1. `k8s/` + namespace + Secret (JWT) + ConfigMap-и (env, monitoring configs).
2. Інфра: Mongo (StatefulSet + replica-set init) + Redis + RabbitMQ (+ PVC) + Services.
3. App Deployments+Services для 5 сервісів (readiness `/health/ready`, `imagePullPolicy`).
4. Gateway + Web Deployments+Services + Ingress/port-forward.
5. Монітеринг: Prometheus/Grafana/Alertmanager Deployments + ConfigMap-mounts + Jaeger.
6. Verify: `kubectl get pods` усі Ready; прогнати той самий smoke (register→login→send через gateway); дашборд Grafana.

**Як почати нову сесію:** відкрити цей репо, сказати «продовжуємо — робимо Kubernetes-розгортання». Ця памʼятка підвантажиться автоматично. Перед стартом можна `docker compose down`, щоб звільнити ресурси (k8s підніме своє). Стан на зараз: усе по інфрі зроблено й CI-green (останнє — [TL-61] alerting). Див. [[observability-metrics]], [[api-gateway]], [[microservices-migration]], [[telegramlike-project-status]].
