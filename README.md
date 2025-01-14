# csad2425ki406yatskivar30

Laboratory works for csad

- **Student:** Yatskiv Adriyan
- **Group:** КІ-406
- **Student number:** 30
- **Game:** Rock, paper, scissors
- **Configuration format:** INI
- **Hardware:** Arduino Uno R3
- **Programming language:** C#

## Table of Contents

- [Version](#version)
- [Build](#build)
- [Run](#run)

## Version
- **Project version:** 3.0.0.0

## Build

### Requirements
- **.NET SDK 8.0**
- **Arduino CLI**
- **Arduino Uno R3** (Microcontroller board)

### Client Side
1. **Clone repository:**
   ```bash
   git clone https://github.com/GrumFX/csad2425ki406yatskivar30.git
   ```
2. **Navigate to client directory:**
   ```bash
   cd csad2425ki406yatskivar30/client/client
   ```
3. **Restore dependencies:**
   ```bash
   dotnet restore
   ```
4. **Build project:**
   ```bash
   dotnet build --configuration Release
   ```

### Server Side
1. **Install Arduino CLI:**
   Download and install [Arduino CLI](https://arduino.github.io/arduino-cli/installation/).
2. **Install Arduino Uno core:**
   ```bash
   arduino-cli core update-index
   arduino-cli core install arduino:avr
   ```
3. **Compile server code:**
   ```bash
   arduino-cli compile --fqbn arduino:avr:uno server/Server.ino --output-dir ./build
   ```

## Run

### Server Side
1. **Upload firmware to Arduino Uno:**
   Connect Arduino to your computer and execute:
   ```bash
   arduino-cli upload -p COM4 --fqbn arduino:avr:uno server/Server.ino
   ```
   **Note:** Replace `COM4` with your Arduino’s actual port.

### Client Side
1. **Run application:**
   ```bash
   dotnet run --project client/client/client.csproj
   ```
2. **Or run compiled file:**
   ```bash
   ./client/client/bin/Release/net8.0/client.exe
   ```