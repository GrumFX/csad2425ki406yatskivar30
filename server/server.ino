#include <Arduino.h>

// Global game state
int choiceOne = -1;
int choiceTwo = -1;

int winsOne = 0;
int winsTwo = 0;
int roundCount = 0;

void sendResult(const String &status) {
  Serial.println(status);
}

void resetRound() {
  choiceOne = -1;
  choiceTwo = -1;
}

void resetGame() {
  winsOne = 0;
  winsTwo = 0;
  roundCount = 0;
  resetRound();
}

// Compare moves: 0=Rock, 1=Paper, 2=Scissors
int compareMoves(int move1, int move2) {
  if (move1 == move2) return 0;  // draw
  if ((move1 == 0 && move2 == 2) ||  
      (move1 == 2 && move2 == 1) ||  
      (move1 == 1 && move2 == 0)) {
    return 1;  // playerOne wins
  }
  else {
    return 2;  // playerTwo wins
  }
}

// Check if someone has already won the entire game
String checkGameOutcome() {
  if (winsOne == 2) {
    return "one_won_game";
  }
  if (winsTwo == 2) {
    return "two_won_game";
  }
  return "";
}

void setup() {
  Serial.begin(9600);
  while (!Serial) {
    ; // Wait for serial to be ready
  }
  resetGame();
}

void loop() {
  // If there's data from the client
  if (Serial.available() > 0) {
    String inMessage = Serial.readStringUntil('\n');
    inMessage.trim(); 

    if (inMessage.startsWith("ping=")) {
      // You can check if it equals "ping=1" if you want to be strict
      sendResult("success");
      return;
    }

    if (inMessage.startsWith("reset=")) {
      // Again, could check if it equals "reset=1"
      resetGame();
      sendResult("game_reset");
      return;
    }

    if (inMessage.startsWith("choices=")) {
      // Extract what's after "choices="
      String data = inMessage.substring(strlen("choices=")); 
      // e.g. "playerOne=0;playerTwo=2"

      // Find the semicolon
      int semicolonIndex = data.indexOf(';');
      if (semicolonIndex == -1) {
        // We expect something like "playerOne=0;playerTwo=1"
        sendResult("error_parsing_xml");
        return;
      }

      // Split into two parts
      String p1Str = data.substring(0, semicolonIndex);        // e.g. "playerOne=0"
      String p2Str = data.substring(semicolonIndex + 1);       // e.g. "playerTwo=2"
      p1Str.trim();
      p2Str.trim();

      // Each part should have "=", so let's parse them
      int eqIndex1 = p1Str.indexOf('=');
      int eqIndex2 = p2Str.indexOf('=');
      if (eqIndex1 == -1 || eqIndex2 == -1) {
        sendResult("error_parsing_xml");
        return;
      }

      String key1 = p1Str.substring(0, eqIndex1);   // "playerOne"
      String val1 = p1Str.substring(eqIndex1 + 1);  // "0" or "1" or "2"
      String key2 = p2Str.substring(0, eqIndex2);   // "playerTwo"
      String val2 = p2Str.substring(eqIndex2 + 1);  // "0" or "1" or "2"

      if (key1 != "playerOne" || key2 != "playerTwo") {
        sendResult("invalid_move");
        return;
      }

      int move1 = val1.toInt();
      int move2 = val2.toInt();

      // Validate moves: must be in [0..2]
      if (move1 < 0 || move1 > 2 || move2 < 0 || move2 > 2) {
        sendResult("invalid_move");
        return;
      }

      // Check if someone already won the game before this round
      String alreadyWon = checkGameOutcome();
      if (alreadyWon != "") {
        // If the game was already decided, any new "round" is invalid
        sendResult("invalid_move");
        return;
      }

      // Save moves (if you still want them tracked)
      choiceOne = move1;
      choiceTwo = move2;

      // Compare
      int result = compareMoves(choiceOne, choiceTwo);
      roundCount++;

      if (result == 1) {
        winsOne++;
      }
      else if (result == 2) {
        winsTwo++;
      }

      // Check if this round caused someone to win the entire game
      String finalStatus = checkGameOutcome();
      if (finalStatus == "") {
        // Game not over yet
        if (result == 0) {
          // draw
          sendResult("draw");
        }
        else if (result == 1) {
          sendResult("one_won_round");
        }
        else {
          sendResult("two_won_round");
        }
        resetRound();
      }
      else {
        // Entire game has a winner
        sendResult(finalStatus);
        resetGame();
      }
      return;
    }
    sendResult("error_parsing_xml");
  }
}
