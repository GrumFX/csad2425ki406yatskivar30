#include <Arduino.h>

// Global game state variables

/** 
 * The choice of Player One for the current round.
 * -1 indicates no choice has been made yet.
 */
int choiceOne = -1;

/** 
 * The choice of Player Two for the current round.
 * -1 indicates no choice has been made yet.
 */
int choiceTwo = -1;

/** 
 * The number of rounds won by Player One.
 */
int winsOne = 0;

/** 
 * The number of rounds won by Player Two.
 */
int winsTwo = 0;

/** 
 * The total number of rounds played in the current game.
 */
int roundCount = 0;

/**
 * Sends a result message back to the client.
 * @param status The status message to send (e.g., "success", "game_reset", etc.).
 */
void sendResult(const String &status) {
  Serial.println(status);
}

/**
 * Resets the current round by clearing the players' choices.
 */
void resetRound() {
  choiceOne = -1;
  choiceTwo = -1;
}

/**
 * Resets the entire game, including the win counters and round count.
 */
void resetGame() {
  winsOne = 0;
  winsTwo = 0;
  roundCount = 0;
  resetRound();
}

/**
 * Compares the moves of Player One and Player Two.
 * @param move1 The move of Player One (0=Rock, 1=Paper, 2=Scissors).
 * @param move2 The move of Player Two (0=Rock, 1=Paper, 2=Scissors).
 * @return 0 for a draw, 1 if Player One wins, 2 if Player Two wins.
 */
int compareMoves(int move1, int move2) {
  if (move1 == move2) return 0;  // draw
  if ((move1 == 0 && move2 == 2) ||  // Rock beats Scissors
      (move1 == 2 && move2 == 1) ||  // Scissors beats Paper
      (move1 == 1 && move2 == 0)) {  // Paper beats Rock
    return 1;  // Player One wins
  }
  return 2;  // Player Two wins
}

/**
 * Checks if a player has won the entire game.
 * @return A string indicating the winner ("one_won_game", "two_won_game") or an empty string if no one has won yet.
 */
String checkGameOutcome() {
  if (winsOne == 2) {
    return "one_won_game";
  }
  if (winsTwo == 2) {
    return "two_won_game";
  }
  return "";
}

/**
 * Arduino setup function, called once at the start.
 * Initializes serial communication and resets the game state.
 */
void setup() {
  Serial.begin(9600);
  while (!Serial) {
    ; // Wait for serial connection to establish
  }
  resetGame();
}

/**
 * Arduino loop function, called continuously.
 * Processes incoming messages from the client and performs game actions.
 */
void loop() {
  if (Serial.available() > 0) {
    String inMessage = Serial.readStringUntil('\n');
    inMessage.trim();

    if (inMessage.startsWith("ping=")) {
      sendResult("success");
      return;
    }

    if (inMessage.startsWith("reset=")) {
      resetGame();
      sendResult("game_reset");
      return;
    }

    if (inMessage.startsWith("choices=")) {
      String data = inMessage.substring(strlen("choices="));
      int semicolonIndex = data.indexOf(';');
      if (semicolonIndex == -1) {
        sendResult("error_parsing_xml");
        return;
      }

      String p1Str = data.substring(0, semicolonIndex);
      String p2Str = data.substring(semicolonIndex + 1);
      p1Str.trim();
      p2Str.trim();

      int eqIndex1 = p1Str.indexOf('=');
      int eqIndex2 = p2Str.indexOf('=');
      if (eqIndex1 == -1 || eqIndex2 == -1) {
        sendResult("error_parsing_xml");
        return;
      }

      String key1 = p1Str.substring(0, eqIndex1);
      String val1 = p1Str.substring(eqIndex1 + 1);
      String key2 = p2Str.substring(0, eqIndex2);
      String val2 = p2Str.substring(eqIndex2 + 1);

      if (key1 != "playerOne" || key2 != "playerTwo") {
        sendResult("invalid_move");
        return;
      }

      int move1 = val1.toInt();
      int move2 = val2.toInt();

      if (move1 < 0 || move1 > 2 || move2 < 0 || move2 > 2) {
        sendResult("invalid_move");
        return;
      }

      String alreadyWon = checkGameOutcome();
      if (alreadyWon != "") {
        sendResult("invalid_move");
        return;
      }

      choiceOne = move1;
      choiceTwo = move2;

      int result = compareMoves(choiceOne, choiceTwo);
      roundCount++;

      if (result == 1) {
        winsOne++;
      } else if (result == 2) {
        winsTwo++;
      }

      String finalStatus = checkGameOutcome();
      if (finalStatus == "") {
        if (result == 0) {
          sendResult("draw");
        } else if (result == 1) {
          sendResult("one_won_round");
        } else {
          sendResult("two_won_round");
        }
        resetRound();
      } else {
        sendResult(finalStatus);
        resetGame();
      }
      return;
    }
    sendResult("error_parsing_xml");
  }
}
