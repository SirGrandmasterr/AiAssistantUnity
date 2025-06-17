

# Project Setup Guide

This guide will walk you through setting up the various components of this project, including the Context Server, OrpheusTTS, L40 LLMs, and the Unity client.

---

## 1. Context Server Setup

The Context Server manages prompts, database interactions, and communication with the LLMs.

### Setup Steps:

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/SirGrandmasterr/context-server.git
    ```

2.  **Switch to the correct branch:**
    ```bash
    cd context-server
    git checkout feature/GeminiOverhaul
    ```

3.  **Configure the environment variables:**
    Open the `.env` file and adjust it as follows, replacing `{L40_server_IP}` with the actual IP address of your L40 server:

    ```ini
    DB_CONN_LINK="mongodb://user:pass@mongodb"
    LLM_SMALL="http://{L40_server_IP}:8012/completions"
    LLM_BIG="http://{L40_server_IP}:8013/completions"
    JWT_SECRET="secret"
    ```

4.  **Start the Docker containers:**
    ```bash
    docker compose up
    ```

5.  **Initialize the database:**
    This command populates the database with prompts and other necessary data from `init_db.json`.
    ```bash
    make make-migrate-up
    ```
    *(Yes, `make make-migrate-up` is intentional.)*

---

## 2. OrpheusTTS Setup

OrpheusTTS handles the text-to-speech functionality for the project.

### Setup Steps:

1.  **Clone the repository:**
    ```bash
    git clone https://gitlab.fokus.fraunhofer.de/fame-wm/metaverse/orpheustts-service/-/tree/main/Signaling?ref_type=heads orpheustts-service
    ```

2.  **Navigate to the Signaling folder:**
    ```bash
    cd orpheustts-service/Signaling
    ```

3.  **Start the Signaling server:**
    ```bash
    go run main.go
    ```

4.  **Open a new terminal** and install the `RealtimeTTS` library:
    ```bash
    pip install "RealtimeTTS[orpheus]"
    ```

5.  **Run the connection script:**
    ```bash
    python ConnectionGemini.py
    ```
    You might encounter missing library errors. Install them as prompted until the script successfully connects to the WebRTC Signaling server you started in the previous step.

---

## 3. L40 Server LLM Setup

You'll need to start three LLMs on your L40 server.

### Setup Steps:

1.  **Log in to your L40 server.**

2.  **Navigate to the `orpheus-tts` directory and start its Docker containers:**
    ```bash
    cd /net/u/phi42197/orpheus-tts
    docker compose up -d
    ```

3.  **Navigate to the `llm-server` directory within `context-server` and start its Docker containers:**
    ```bash
    cd /net/u/phi42197/context-server/llm-server
    docker compose up -d
    ```

---

## 4. Unity Client Setup

The Unity client is the frontend for interacting with the system.

### Setup Steps:

1.  **Open the Unity project.**
2.  **Select the `StartMenu` Scene.**
3.  **Click Play.**

### Interaction:

* Press the **"E" button** to initiate talking.
* Walk towards the Assistant in the Unity environment to put him into conversation mode (his head will turn blue).

### Available Actions:

* Currently available actions are defined in the `context-server`'s `init_db.json` file.
* If you make changes to `init_db.json`, you'll need to delete and re-migrate the database using the `make make-migrate-up` command in the `context-server` directory.
* The **"Brain" Script** in Unity controls when specific actions are available.

---
