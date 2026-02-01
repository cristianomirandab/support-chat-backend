#**Requirements**

- PowerShell (Windows)

- No database required

- Native OpenAPI (no Swagger / Swashbuckle)


How to run the project

From the repository root:

```dotnet restore
dotnet run --project src/SupportChat.Api
```


The API will start and expose the OpenAPI contract at:
GET /openapi/v1.json


| Method | Endpoint         | Description                |
| ------ | ---------------- | -------------------------- |
| POST   | /chats           | Creates a chat session     |
| GET    | /chats/{id}      | Retrieves chat status      |
| GET    | /chats/{id}/poll | Client polling (heartbeat) |


Test scripts

The project includes two PowerShell scripts to validate the requirements:

scripts/
 ├── test.ps1      # main automated tests
 └── rr_test.ps1   # isolated Round Robin test


#**Main automated tests (test.ps1)**

This script automatically validates:

OpenAPI availability

Chat creation (POST /chats)

Dispatcher assigning agents

Polling keeps the chat active

Missing polls cause Inactive

Overflow behavior (simulation mode)

Run
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -BaseUrl "http://localhost:5000"

Important note (Overflow simulation)

To make overflow testing easier with a small number of chats, the project supports a simulation mode via configuration.

In appsettings.json (testing only):

"Testing": {
  "ForceOfficeHours": true,
  "MainMaxQueueOverride": 2,
  "UsePressureForOverflowTrigger": true
}

After testing, this block can be removed to restore the real PDF behavior.

Simple Round Robin test (rr_test.ps1)

This test validates only the Round Robin logic, in a manual and controlled way, exactly as described in the PDF.

Preparation (manual and simple)

1. In Program.cs, adjust the seed to include only two agents in the same team:

store.Upsert(new Agent {
    Team = TeamId.TeamA,
    Seniority = Seniority.Junior,
    AcceptingNewChats = true,
    ShiftStart = now.AddHours(-1),
    ShiftEnd = now.AddHours(7)
});

store.Upsert(new Agent {
    Team = TeamId.TeamA,
    Seniority = Seniority.Senior,
    AcceptingNewChats = true,
    ShiftStart = now.AddHours(-1),
    ShiftEnd = now.AddHours(7)
});

2. Ensure that POST /chats always creates chats for TeamA:

- Either configure office hours to cover the entire day or

- Temporarily force mainTeam = TeamId.TeamA in the controller

3. Remove any queue overrides from appsettings.json and restart the API.

#Run the Round Robin test

powershell -ExecutionPolicy Bypass -File .\scripts\rr_test.ps1 -BaseUrl "http://localhost:5000"

*Expected result (PDF example)*

- 5 chats created

- Expected distribution:

    - Junior → 4 chats

    - Senior → 1 chat

*The script prints:*

- Distribution by AssignedAgentId

- A warning if the algorithm behaves as simple round robin (e.g., 3/2)


Design notes

- The “queue” described in the PDF is modeled as team backlog:

- Queued + Assigned + Active

- This ensures consistent behavior even with a fast dispatcher.

- The rule maxQueue = capacity × 1.5 is strictly respected.

- Overflow is activated only:

    - during office hours

    - when the main queue reaches its limit

- Persistence is fully in-memory.

- Simulation mode is opt-in and isolated by configuration.