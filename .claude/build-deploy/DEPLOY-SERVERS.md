# Build & Deploy — servers and deployment

Deployment notes and commands.

Deploy script (server and directory)

```bash
# server and directory (rebuilds ARM64, installs both units, restarts them)
MPH_SERVER_HOST=net.livetek.fr MPH_SERVER_USER=livetek \
  MPH_SERVER_PASS="$(read -rsp 'pi password: ' p; echo "$p")" ./deploy-server.sh
# MPH_DEPLOY_MASTER=0 to leave the directory alone
```

Publish commands (Windows client and server)

```bash
# Windows client
dotnet publish src/MphRead/MphRead.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -o publish/win-x64

# Windows dedicated server
dotnet publish src/MphRead/MphRead.csproj -c Release -r win-x64 \
  -p:MphReadServer=true --self-contained true -p:PublishSingleFile=true \
  -o publish/win-x64-server
```

Notes

- The exe may be locked by a running game; write `MphRead.new.exe` then `mv`.
- Any protocol change requires server and every client to be the same build. `NetConfig.ProtocolVersion` is **6** in this build (it was 5 in v0.6.0, 6 from v0.7.0) — a mismatched client is refused outright at Hello. Deploy the server before handing out a client built against a new version.

## The fleet

Four boxes, not one. `deploy-server.sh` only ever touches the Pi -- the three
Azure VMs are deployed by hand, and forgetting them is easy because the Pi is
the one with a name anybody says out loud. **Ask the directory rather than
remembering**, which is the only inventory that is never out of date:

```bash
FruityPrime -servers -master net.livetek.fr -masterport 27889
```

| Box | Region | Public relay | Also |
|---|---|---|---|
| `raspberrypi`, 89.160.162.50 (ARM64) | home | `mphread-server`, 27888 | `mphread-master` 27889 (the directory), `fruityprime-sim` 27890 |
| `vm-test-01`, 13.78.14.98 | japaneast | `mphread-server`, 27888 | `fruityprime-sim` 27890 |
| `vm-test-02`, 20.16.135.109 | westeurope | `mphread-server`, 27888 | |
| `vm-test-03`, 20.230.186.218 | westus2 | `mphread-server`, 27888 | |

Every Azure relay is `/opt/fruityprime-server/FruityPrime`, unit
`mphread-server`, user `fpserver`, and all three take the same x86-64 build:

```bash
dotnet publish src/MphRead -c Release -r linux-x64 -p:MphReadServer=true \
  --self-contained true -p:PublishSingleFile=true -o publish/server-x64
```

**All three Azure boxes are reached only through the Pi** -- port 22 is closed
to the internet on every one of them, not just Japan, and they share one
resource group (`RG-SANDBOX-PERSONAL`, subscription
`03f0ad6d-b361-4a4c-a460-7bc5d94663e4`) and one login. Ask the VM what it is
rather than guessing from the IP:

```bash
curl -s -H Metadata:true \
  "http://169.254.169.254/metadata/instance/compute?api-version=2021-02-01"
```

## The simulation-authority servers (test, deployed 2026-09-09)

`-simulate` (`.claude/multiplayer/NETWORK-SERVERAUTH.md`) needs the game files
beside the binary, so it is deployed by hand rather than by
`deploy-server.sh`. Two boxes carry one, **on port 27890, unlisted, alongside
the production relay on 27888 which is untouched**:

| Box | Path | Unit |
|---|---|---|
| the Pi (ARM64) | `~/mphread-sim/` | `fruityprime-sim.service`, user `livetek` |
| Japan, `13.78.14.98` (x86-64) | `/opt/fruityprime-sim/` | `fruityprime-sim.service`, user `fpserver` |

**A box can carry both**, and Japan does: the relay on 27888 and the sim on
27890 are separate units, separate directories and separate binaries. Deploying
one is not deploying the other, and the relay is the one players join.

Both units carry two flags that are not optional here:

- **`-nomaster`** — these are test servers and must not appear in the browser.
- **`-noautoupdate`** — the newest GitHub release does not carry the
  server-authority code, so an auto-update would silently put the relay build
  back. Remove it once this work is released.

The 52 MB pruned file set and `paths.txt` sit beside each binary. See
NETWORK-SERVERAUTH.md for what is in it and why.

**The Japan box is only reachable through the Pi**, which is the SSH jump host
— port 22 is not open to the internet on it:

```bash
sshpass -e ssh -o ProxyCommand="sshpass -p <pi-pass> ssh -W %h:%p livetek@net.livetek.fr" \
  livetek@13.78.14.98
```

**27890/UDP is open on Japan now, and is the test server anybody means when
they say "Japan"** -- `13.78.14.98:27890`, simulating. Re-checked 2026-09-09
from the WSL box: `-netcheck 13.78.14.98 -port 27890` joins, takes a slot and
gets snapshots, at 267-278 ms.

It was not always. This file used to say the NSG opened 27888 alone, which was
measured and was true at the time: the service was listening on 0.0.0.0:27890
and stepping the simulation while nothing outside could reach it, and 27889
was equally closed, so the rule was a single port and not the 27888-28999 range
it is assumed to be. If a fourth port is ever needed the fix is the same cloud
control-plane change, from a machine with the Azure CLI:

```bash
az network nsg rule create --resource-group <rg> --nsg-name <nsg> \
  --name fruityprime-sim --priority 1010 --protocol Udp \
  --destination-port-ranges 27890 --access Allow --direction Inbound
```
