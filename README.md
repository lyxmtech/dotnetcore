# HelloWorld — GET /hello + event fan-out (in-process or via RabbitMQ)

```
HelloWorld.sln
├── BusCore/              # HelloRequested + IEventBus + AddEventBus (flag logic)
├── DbCore/               # IDbConnectionFactory + SQLite connection + schema bootstrap
├── DataCore/             # shared repositories/schemas (plain class library)
│   ├── Requests/         # RequestsRepository + RequestsSchema (requests table)
│   └── Audit/            # AuditRepository + AuditSchema (audit + audit_stats tables)
├── HelloService/         # GET /hello → publishes HelloRequested (port 5000)
├── AuditService/         # consumes HelloRequested → logs + persists (port 5001)
├── NotifyService/        # consumes HelloRequested → "notifies"
├── DemoHost/             # Option A: all three in ONE process (port 5010)
└── k8s/                  # Option B on Kubernetes
    ├── 00-namespace.yaml
    ├── 10-rabbitmq.yaml  # Secret + Service + StatefulSet (broker)
    ├── 20-config.yaml    # ConfigMap (EventBus flag + RabbitMq__* settings) + PVCs
    └── 30-services.yaml  # Deployments + Services for hello/audit/notify
```

### Why `DataCore` is a class library (and not two Web projects)

`DemoHost` needs the same persistence code as `HelloService` and `AuditService`.
Referencing those Web SDK projects directly makes MSBuild merge each referenced
project's content items into the parent's publish output, so three projects ship
their own `appsettings.json` / `appsettings.Development.json` and the publish
fails with **NETSDK1152** ("Duplicate 'appsettings.json' items were included").
`ExcludeAssets="content"` does not suppress that merge.

Extracting the repositories into `DataCore` (a plain `Microsoft.NET.Sdk` class
library) fixes the collision without changing who owns which database:

| Deployment | `requests` table | `audit` / `audit_stats` tables |
|---|---|---|
| Option A (`DemoHost`) | `db/demo.db` | `db/demo.db` (shared file) |
| Option B (`HelloService`) | `db/hello.db` | — |
| Option B (`AuditService`) | — | `db/audit.db` |

`DbCore` owns the connection factory, the `Database:Path` setting and the
schema bootstrap; `DataCore` owns the Dapper repositories and table schemas on
top of it. `DbCore` and `DataCore` are referenced by every service that needs
to persist, and none of them is a Web SDK project, so no configuration files
can collide.

`BusCore` owns the `EventBus` flag in exactly one place (`EventBusSetup.AddEventBus`):
- `EventBus=InMemory` → `InMemoryEventBus` (no broker)
- `EventBus=RabbitMQ` → `RabbitMqEventBus` (MassTransit + RabbitMQ)
- `EventBus=SQS` → `SqsEventBus` (MassTransit + Amazon SQS/SNS; AWS in production, LocalStack for local testing)

The RabbitMQ broker location comes from the `RabbitMq` configuration section
(`RabbitMq__Host`, `RabbitMq__Port`, `RabbitMq__Username`, `RabbitMq__Password`,
`RabbitMq__VirtualHost`), so the same image runs under Docker Compose and
Kubernetes without a rebuild.

The SQS settings come from the `Sqs` section (`Sqs__Region`, plus `Sqs__ServiceUrl`,
`Sqs__AccessKey`, `Sqs__SecretKey` for LocalStack). In AWS, leave ServiceUrl empty
and let the default credential chain (ECS task role) authenticate.

## Option A — in-process fan-out

```powershell
cd d:\AI-sandbox\DotNetCore
dotnet run --project DemoHost
# then in another shell:
curl "http://localhost:5010/hello?name=Alice"
# console shows:
# [AUDIT] ... hello requested by Alice
# [NOTIFY] ... Sending welcome notification to Alice
```

Or via Docker:

```powershell
docker compose up -d --build
curl "http://localhost:5000/hello?name=Alice"
```

## Option B — MassTransit + RabbitMQ across containers

```powershell
docker compose -f docker-compose.option-b.yml up -d --build
curl "http://localhost:5000/hello?name=Alice"

# verify both consumers received the event
docker logs dotnetcore-audit-1  | Select-String AUDIT
docker logs dotnetcore-notify-1 | Select-String NOTIFY
# [AUDIT]  ... hello requested by Alice
# [NOTIFY] Sending welcome notification to Alice
```

RabbitMQ UI: http://localhost:15672 (guest / guest)

Stop and clean up:

```powershell
docker compose -f docker-compose.option-b.yml down
```

## Persistence (SQLite + Dapper)

Every service that stores data owns its own SQLite file, configured through
`Database:Path` (`Database__Path` as an environment variable). Locally the file
lives under `db/`; in containers and on Kubernetes it lives on the mounted
volume at `/data`.

| Endpoint | Returns | Available in |
|---|---|---|
| `GET /hello?name=...` | greeting, and persists a `requests` row | `HelloService`, `DemoHost` |
| `GET /requests` | the persisted `requests` rows | `HelloService`, `DemoHost` |
| `GET /audit` | the persisted `audit` rows | `AuditService`, `DemoHost` |
| `GET /audit/stats` | the `audit_stats` rollup (total) | `AuditService`, `DemoHost` |

```powershell
# Option A (single shared database)
curl "http://localhost:5010/hello?name=Alice"
curl "http://localhost:5010/requests"
curl "http://localhost:5010/audit/stats"

# Option B (one database per service)
curl "http://localhost:5000/hello?name=Alice"   # -> hello.db
curl "http://localhost:5000/requests"
curl "http://localhost:5001/audit"              # -> audit.db
curl "http://localhost:5001/audit/stats"
```

The data survives restarts because it lives in a file, not in memory:
`docker compose restart` keeps the named volumes, and the local `db/*.db`
files are reused on the next `dotnet run`.

## Option B on Kubernetes

The same Option B topology, deployed to a cluster (verified against Docker
Desktop's built-in Kubernetes, `docker-desktop` context).

```powershell
cd d:\AI-sandbox\DotNetCore

# 1. Build the service images
docker compose -f docker-compose.option-b.yml build

# 2. Load them into every node's containerd (kind-style clusters need this;
#    Docker Desktop shares the host daemon, so it may already be visible)
./k8s/load-images.ps1 -Context docker-desktop

# 3. Apply the manifests
kubectl --context docker-desktop apply -f k8s/

# 4. Verify
kubectl --context docker-desktop -n dotnetcore get pods
# NAME                      READY   STATUS    RESTARTS   AGE
# audit-79f99f7574-zv629    1/1     Running   0          52s
# hello-5d75b5bb95-kprzw    1/1     Running   0          52s
# notify-747cc5d7d4-hd97j   1/1     Running   0          52s
# rabbitmq-0                1/1     Running   0          53s

# 5. Publish an event and watch both consumers receive it
kubectl --context docker-desktop -n dotnetcore port-forward svc/hello 18080:80
curl "http://localhost:18080/hello?name=Alice"

kubectl --context docker-desktop -n dotnetcore logs deploy/audit  | Select-String AUDIT
kubectl --context docker-desktop -n dotnetcore logs deploy/notify | Select-String NOTIFY
# [AUDIT]  ... hello requested by Alice
# [NOTIFY] Sending welcome notification to Alice

# RabbitMQ management UI
kubectl --context docker-desktop -n dotnetcore port-forward svc/rabbitmq 15672:15672
# http://localhost:15672  (dotnetcore / dotnetcore-rabbit)
```

Clean up:

```powershell
kubectl --context docker-desktop delete -f k8s/
```

### Kubernetes layout

| Resource | Name | Notes |
|---|---|---|
| Namespace | `dotnetcore` | isolates the demo |
| Secret | `rabbitmq-credentials` | broker user/password (demo values — replace for real use) |
| StatefulSet + Service | `rabbitmq` | headless Service, PVC for data, `loopback_users []` so remote pods can connect |
| ConfigMap | `eventbus-config` | `EventBus=RabbitMQ`, `RabbitMq__Host=rabbitmq`, URLs, env |
| Deployment + Service | `hello` | publisher, `NodePort` 30081 |
| Deployment + Service | `audit` | consumer |
| Deployment + Service | `notify` | consumer |

Queue names match the Compose run: `audit-audit` and `notify-notify`.

Note: NodePorts are not reachable from the Windows host on Docker Desktop
(the node IPs live inside Docker's network). Use `kubectl port-forward` as shown
above.

## Option C — MassTransit + Amazon SQS/SNS (LocalStack locally)

```powershell
docker compose -f docker-compose.sqs.yml up -d --build
curl "http://localhost:5000/hello?name=Alice"

# verify both consumers received the event
docker logs dotnetcore-audit-1  | Select-String AUDIT
docker logs dotnetcore-notify-1 | Select-String NOTIFY
```

In AWS the task definitions set `EventBus=SQS` and `Sqs__Region=us-east-2` only;
credentials come from the ECS task role (needs `sqs:*` and `sns:*` on the
resources MassTransit creates).

## Per-service processes without a broker (shows the limitation)

```powershell
dotnet run --project HelloService   # :5000
dotnet run --project AuditService   # :5001
dotnet run --project NotifyService  # :5002
curl "http://localhost:5000/hello?name=Alice"
# Returns Hello but audit/notify see nothing — separate InMemoryEventBus instances.
# Option B fixes this with a real broker.
```

