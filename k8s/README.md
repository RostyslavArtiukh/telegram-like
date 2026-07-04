# Kubernetes deployment

A one-namespace (`telegramlike`) mirror of `docker-compose.yml`. Built with plain
manifests + Kustomize (no Helm). The kustomization lives at the **repo root** so it
can pull `monitoring/*` into ConfigMaps.

## Prerequisites
- A local cluster. Easiest: **Docker Desktop → Settings → Kubernetes → Enable Kubernetes**.
  Docker Desktop's k8s shares the docker daemon, so the locally-built
  `telegramlike-*:latest` images are visible to the cluster (manifests use
  `imagePullPolicy: IfNotPresent` — nothing is pulled from a registry).
- Build the images first if you haven't: `docker compose build`

## Deploy
```bash
kubectl apply -k .          # from the repo root
kubectl get pods -n telegramlike -w
```
The `mongo-rs-init` Job initiates the single-node replica set `rs0` (needed for
transactions). Services may restart a couple of times until Mongo is a replica set
and RabbitMQ is up — that's expected; they settle once dependencies are Ready.

## Access
- Web UI: **http://localhost:30080** (NodePort)
- Everything else via port-forward, e.g.:
  ```bash
  kubectl port-forward -n telegramlike svc/gateway 8090:8080
  kubectl port-forward -n telegramlike svc/grafana 3000:3000
  kubectl port-forward -n telegramlike svc/prometheus 9090:9090
  kubectl port-forward -n telegramlike svc/alertmanager 9093:9093
  kubectl port-forward -n telegramlike svc/jaeger 16686:16686
  kubectl port-forward -n telegramlike svc/rabbitmq 15672:15672
  ```

## Tear down
```bash
kubectl delete -k .
# PVCs (mongo data, web DataProtection keys) are kept; remove explicitly if wanted:
kubectl delete pvc --all -n telegramlike
```

## Notes
- Service DNS names match the compose service names, so `prometheus.yml`, the YARP
  gateway destinations and the web `Gateway__BaseUrl` are reused unchanged.
- The shared `ServiceAuth__JwtSecret` is a k8s `Secret` (`app-secrets`); shared env
  is in the `app-config` ConfigMap; per-service DB names are set on each Deployment.
