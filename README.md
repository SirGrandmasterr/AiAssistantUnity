Setup:

Context Server:
1. Clone the Context Server somewhere: https://github.com/SirGrandmasterr/context-server
2. Go to branch feature/GeminiOverhaul
3. Adjust .env file like so, exchanging "L40_server_IP" for the actual IP:

DB_CONN_LINK="mongodb://user:pass@mongodb"
LLM_SMALL="http://{L40_server_IP}:8012/completions"
LLM_BIG="http://{L40_server_IP}:8013/completions"
JWT_SECRET="secret"

4. enter docker compose up
5. enter make make-migrate-up (yes, that's make twice because I can) to fill the database with all the prompts etc. from init_db.json

OrpheusTTS:
1. Clone the Repo from Fraunhofer Gitlab: https://gitlab.fokus.fraunhofer.de/fame-wm/metaverse/orpheustts-service/-/tree/main/Signaling?ref_type=heads
2. Move into the Signaling folder
3. run "go run main.go" (didn't make a Dockerfile for that one yet)
4. open a new terminal
5. run "pip install RealtimeTTS[orpheus]"
6. Then try to execute "python ConnectionGemini.py". It's gonna fail because of non-installed libraries, install those
7. At some point it should work and connect itself to the webRTC Signaling server you started with the main.go file.

L40:
We're going to start 3 LLMs situated on the L40 Server. For that, well, log into the L40 server.
1. /net/u/phi42197/ has 2 folders: orpheus-tts and context-server
2. cd into orpheus and enter docker compose up -d
3. cd back to /net/u/phi42197/ and into context-server/llm-server/
4. enter docker compose up -d

Finally, in Unity, select the StartMenu Scene and click play. "E" button is talking. Walking to the Assistant puts him in conversation mode (Head blue).
Currently available actions are listed in the context-server's "init_db.json". If you change anything, use the makefile command to delete the database and migrate again.
The "Brain" Script in Unity controls when which action is available.
