using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Media;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace RockPapperScissorsWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// This class represents the main window of the Rock-Paper-Scissors WPF application.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields

        private SerialPort serialPort;
        private Button[,] buttons;
        private GameMode currentGameMode;
        private bool isGameActive;
        private string currentPlayer;
        private Random random;

        private string configPath = "config.ini";

        private bool isComputerTurn;
        private CancellationTokenSource computerGameCts;
        private int baudRate;

        private int? currentChoicePlayerOne;
        private int? currentChoicePlayerTwo;

        // Optionally: store the list of round information.
        private readonly List<RoundInfo> roundHistory = new List<RoundInfo>();

        /// <summary>
        /// Represents the time of the last move made.
        /// </summary>
        private DateTime lastMoveTime = DateTime.MinValue;

        /// <summary>
        /// A time span that enforces a delay between moves.
        /// </summary>
        private readonly TimeSpan moveDelay = TimeSpan.FromSeconds(0.5);

        #endregion

        /// <summary>
        /// Information about a single round of the game.
        /// </summary>
        private class RoundInfo
        {
            /// <summary>
            /// Gets or sets the round number.
            /// </summary>
            public int RoundNumber { get; set; }

            /// <summary>
            /// Gets or sets Player One's move (Rock, Paper, or Scissors).
            /// </summary>
            public string PlayerOneMove { get; set; }

            /// <summary>
            /// Gets or sets Player Two's move (Rock, Paper, or Scissors).
            /// </summary>
            public string PlayerTwoMove { get; set; }

            /// <summary>
            /// Gets or sets the outcome of the round.
            /// </summary>
            public string RoundResult { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class 
        /// and sets up the initial state of the game.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            InitializeGame();
            LoadConfiguration();
            RefreshPorts();
        }

        #region Initialization

        /// <summary>
        /// Prepares the game data structures and creates UI elements for player choices.
        /// </summary>
        private void InitializeGame()
        {
            buttons = new Button[1, 3];
            random = new Random();
            CreateChoices();
        }

        /// <summary>
        /// Dynamically creates the Rock, Paper, and Scissors buttons and adds them to the UI.
        /// </summary>
        private void CreateChoices()
        {
            for (int i = 0; i < 3; i++)
            {
                var button = new Button
                {
                    FontSize = 30,
                    Margin = new Thickness(5),
                    IsEnabled = false
                };
                button.Click += GameButton_Click;
                button.Tag = i;

                Grid.SetRow(button, 0);
                Grid.SetColumn(button, i);

                GameGrid.Children.Add(button);
                buttons[0, i] = button;
            }

            // Label the three buttons.
            buttons[0, 0].Content = "💎 Rock";
            buttons[0, 1].Content = "📄 Paper";
            buttons[0, 2].Content = "✂ Scissors";
        }

        #endregion

        #region UI and Configuration

        /// <summary>
        /// Enables or disables the choice buttons based on the <paramref name="isActive"/> parameter.
        /// </summary>
        /// <param name="isActive">If true, makes the buttons clickable; otherwise, disables them.</param>
        private void SetGameBoardActive(bool isActive)
        {
            foreach (var button in buttons)
            {
                button.IsEnabled = isActive;
            }
        }

        /// <summary>
        /// Loads the available COM ports from the system and populates the <see cref="PortComboBox"/>.
        /// </summary>
        private void RefreshPorts()
        {
            PortComboBox.Items.Clear();
            foreach (string port in SerialPort.GetPortNames())
            {
                PortComboBox.Items.Add(port);
            }
        }

        /// <summary>
        /// Reads the configuration settings from the INI file (if it exists).
        /// Sets UI elements (port, baud rate, game mode) to the saved values.
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var lines = File.ReadAllLines(configPath);
                    var configDict = new Dictionary<string, string>();

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var val = parts[1].Trim();
                            configDict[key] = val;
                        }
                    }

                    if (configDict.TryGetValue("port", out string savedPort))
                    {
                        var availablePorts = SerialPort.GetPortNames();
                        if (availablePorts.Contains(savedPort))
                        {
                            PortComboBox.SelectedItem = savedPort;
                        }
                    }

                    if (configDict.TryGetValue("baudRate", out string rate))
                    {
                        switch (rate)
                        {
                            case "4800":
                                BaudRateComboBox.SelectedIndex = 0;
                                baudRate = 4800;
                                break;
                            case "9600":
                                BaudRateComboBox.SelectedIndex = 1;
                                baudRate = 9600;
                                break;
                            case "19200":
                                BaudRateComboBox.SelectedIndex = 2;
                                baudRate = 19200;
                                break;
                            case "38400":
                                BaudRateComboBox.SelectedIndex = 3;
                                baudRate = 38400;
                                break;
                            case "57600":
                                BaudRateComboBox.SelectedIndex = 4;
                                baudRate = 57600;
                                break;
                        }
                    }

                    if (configDict.TryGetValue("gameMode", out string mode))
                    {
                        switch (mode)
                        {
                            case "PvP":
                                GameModeComboBox.SelectedIndex = 0;
                                currentGameMode = GameMode.PlayerVsPlayer;
                                break;
                            case "PvC":
                                GameModeComboBox.SelectedIndex = 1;
                                currentGameMode = GameMode.PlayerVsComputer;
                                break;
                            case "CvC":
                                GameModeComboBox.SelectedIndex = 2;
                                currentGameMode = GameMode.ComputerVsComputer;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the current configuration (port, baud rate, game mode) to the INI file.
        /// </summary>
        private void SaveConfiguration()
        {
            try
            {
                var lines = new List<string>
                {
                    $"port={PortComboBox.SelectedItem?.ToString() ?? ""}",
                    $"baudRate={baudRate}",
                    $"gameMode={GetGameModeString()}"
                };

                File.WriteAllLines(configPath, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the string representation of the <see cref="GameMode"/> for saving or display.
        /// </summary>
        /// <returns>The string that represents the current <see cref="GameMode"/>.</returns>
        private string GetGameModeString()
        {
            return currentGameMode switch
            {
                GameMode.PlayerVsPlayer => "PvP",
                GameMode.PlayerVsComputer => "PvC",
                GameMode.ComputerVsComputer => "CvC",
                _ => "PvC"
            };
        }

        #endregion

        #region Event Handlers (UI)

        /// <summary>
        /// Event handler for the "Start Game" button click.
        /// Sets up the serial port, sends a reset command to the server, and starts the game loop if necessary.
        /// </summary>
        /// <param name="sender">The source of the event (the StartGame button).</param>
        /// <param name="e">Event arguments.</param>
        private async void StartGame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadConfiguration();

                var selectedPort = PortComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedPort))
                {
                    MessageBox.Show("Please select a COM port.");
                    return;
                }

                if (serialPort != null && serialPort.IsOpen)
                    serialPort.Close();

                serialPort = new SerialPort(selectedPort, baudRate)
                {
                    ReadTimeout = 2000,
                    WriteTimeout = 2000
                };

                serialPort.Open();

                // Send a reset command to the server.
                string response = await SendMessageWithTimeout("reset=1", 2000);
                if (response == null || !response.Contains("game_reset"))
                {
                    MessageBox.Show("Failed to reset the game.");
                    return;
                }

                ResetGame();
                SaveConfiguration();
                SetGameBoardActive(true);

                if (currentGameMode == GameMode.ComputerVsComputer)
                {
                    computerGameCts = new CancellationTokenSource();
                    await PlayComputerGame(computerGameCts.Token);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for each Rock/Paper/Scissors button click.
        /// Captures the player's choice and then triggers the logic to finalize or continue the round.
        /// </summary>
        /// <param name="sender">The source of the event (one of the game buttons).</param>
        /// <param name="e">Event arguments.</param>
        private async void GameButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isGameActive || isComputerTurn || (DateTime.Now - lastMoveTime) < moveDelay)
                return;

            var button = (Button)sender;
            var position = (int)button.Tag;

            lastMoveTime = DateTime.Now;
            await MakeChoice(position);

            if (isGameActive && currentGameMode == GameMode.PlayerVsComputer && !isComputerTurn)
            {
                isComputerTurn = true;
                await MakeComputerMove();
                isComputerTurn = false;
            }
        }

        /// <summary>
        /// Event handler for the port ComboBox selection change.
        /// Saves the configuration immediately when the user selects a new port.
        /// </summary>
        private void PortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveConfiguration();
        }

        /// <summary>
        /// Event handler for the BaudRateComboBox selection change.
        /// Parses and saves the selected baud rate to the configuration.
        /// </summary>
        private void BaudRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BaudRateComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (int.TryParse(selectedItem.Content.ToString(), out int selectedBaudRate))
                {
                    baudRate = selectedBaudRate;
                    SaveConfiguration();
                }
            }
        }

        /// <summary>
        /// Event handler for the GameModeComboBox selection change.
        /// Updates <see cref="currentGameMode"/> and saves the new mode to the configuration.
        /// </summary>
        private void GameModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameModeComboBox.SelectedIndex >= 0)
            {
                currentGameMode = GameModeComboBox.SelectedIndex switch
                {
                    0 => GameMode.PlayerVsPlayer,
                    1 => GameMode.PlayerVsComputer,
                    2 => GameMode.ComputerVsComputer,
                    _ => GameMode.PlayerVsComputer
                };
                SaveConfiguration();
            }
        }

        #endregion

        #region Game Logic

        /// <summary>
        /// Resets the game state to defaults and updates UI elements accordingly.
        /// </summary>
        private void ResetGame()
        {
            isGameActive = true;
            currentPlayer = "One";
            StatusText.Text = $"Current player: {currentPlayer}";
            ResultText.Text = "";
            isComputerTurn = false;

            currentChoicePlayerOne = null;
            currentChoicePlayerTwo = null;
            roundHistory.Clear();
        }

        /// <summary>
        /// Wraps writing a message to the serial port and waiting for a response with a timeout.
        /// </summary>
        /// <param name="message">The message to send to the server.</param>
        /// <param name="timeout">The maximum time to wait (in milliseconds) for a response.</param>
        /// <returns>The response from the server, or null if there was a timeout or other error.</returns>
        private async Task<string> SendMessageWithTimeout(string message, int timeout)
        {
            try
            {
                var cts = new CancellationTokenSource(timeout);
                serialPort.WriteLine(message);

                string response = await Task.Run(() =>
                {
                    return serialPort.ReadLine();
                }, cts.Token);

                return response;
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Server response timed out.");
                SetGameBoardActive(false);
                computerGameCts?.Cancel();
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error communicating with server: {ex.Message}");
                SetGameBoardActive(false);
                computerGameCts?.Cancel();
                return null;
            }
        }

        /// <summary>
        /// Called when the user (or the computer) selects Rock/Paper/Scissors.
        /// Sets the appropriate player's choice, updates <see cref="currentPlayer"/>, and sends choices to the server if both are selected.
        /// </summary>
        /// <param name="choice">The choice index (0=Rock, 1=Paper, 2=Scissors).</param>
        private async Task MakeChoice(int choice)
        {
            if (!isGameActive) return;

            if (currentPlayer == "One") currentChoicePlayerOne = choice;
            else if (currentPlayer == "Two") currentChoicePlayerTwo = choice;

            currentPlayer = currentPlayer == "One" ? "Two" : "One";
            StatusText.Text = $"Current player: {currentPlayer}";

            await SendChoicesToServer();
        }

        /// <summary>
        /// Sends the accumulated choices to the server if both players have already chosen.
        /// If the server returns a result, processes it.
        /// </summary>
        private async Task SendChoicesToServer()
        {
            if (currentChoicePlayerOne.HasValue && currentChoicePlayerTwo.HasValue)
            {
                // e.g. "choices=playerOne=0;playerTwo=2"
                string roundMessage =
                    $"choices=playerOne={currentChoicePlayerOne.Value};playerTwo={currentChoicePlayerTwo.Value}";

                string response = await SendMessageWithTimeout(roundMessage, 2000);
                if (response != null)
                {
                    ProcessServerResponse(response);
                }

                currentChoicePlayerOne = null;
                currentChoicePlayerTwo = null;
            }
        }

        /// <summary>
        /// Processes the server's response to determine the round/game outcome and updates the UI accordingly.
        /// </summary>
        /// <param name="response">The raw response string from the server.</param>
        private void ProcessServerResponse(string response)
        {
            if (response.Contains("invalid_move"))
            {
                MessageBox.Show("Invalid move!");
                return;
            }
            if (response.Contains("error_parsing_xml"))
            {
                MessageBox.Show("Server could not parse the XML properly.");
                return;
            }

            // Overall game outcomes:
            if (response.Contains("one_won_game"))
            {
                GameOver("Player One wins the entire game!");
                ShowRoundMoves("Player One wins the entire game!");
                return;
            }
            else if (response.Contains("two_won_game"))
            {
                GameOver("Player Two wins the entire game!");
                ShowRoundMoves("Player Two wins the entire game!");
                return;
            }
            // Single round outcomes:
            else if (response.Contains("draw"))
            {
                ShowRoundMoves("draw");
                return;
            }
            else if (response.Contains("one_won_round"))
            {
                ShowRoundMoves("Player One won in round");
                return;
            }
            else if (response.Contains("two_won_round"))
            {
                ShowRoundMoves("Player Two won in round");
                return;
            }
            else
            {
                MessageBox.Show($"Unexpected server response: {response}");
            }
        }

        /// <summary>
        /// Displays a message box with the moves and results for the current round.
        /// Also appends the round info to the <see cref="roundHistory"/>.
        /// </summary>
        /// <param name="roundResult">A string describing the outcome (e.g., "draw", "Player One won").</param>
        private void ShowRoundMoves(string roundResult)
        {
            string p1 = ChoiceToString(currentChoicePlayerOne);
            string p2 = ChoiceToString(currentChoicePlayerTwo);

            var roundInfo = new RoundInfo
            {
                RoundNumber = roundHistory.Count + 1,
                PlayerOneMove = p1,
                PlayerTwoMove = p2,
                RoundResult = roundResult
            };
            roundHistory.Add(roundInfo);

            MessageBox.Show(
                $"[ ROUND #{roundInfo.RoundNumber} COMPLETED ]\n" +
                $"---------------------------------\n" +
                $"• PLAYER ONE => {p1}\n" +
                $"• PLAYER TWO => {p2}\n" +
                $"---------------------------------\n" +
                $"OUTCOME => {roundResult}\n"
            );
        }

        /// <summary>
        /// Converts the numeric move choice (0,1,2) to a string ("Rock", "Paper", "Scissors").
        /// </summary>
        /// <param name="choice">The player's choice in numeric form.</param>
        /// <returns>A string representing the move. "None" if no choice is set.</returns>
        private string ChoiceToString(int? choice)
        {
            if (!choice.HasValue) return "None";

            switch (choice.Value)
            {
                case 0: return "Rock";
                case 1: return "Paper";
                case 2: return "Scissors";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Handles the computer-vs-computer game loop. Each player picks randomly with a short delay.
        /// </summary>
        /// <param name="ct">A cancellation token to stop the computer loop if the game ends.</param>
        private async Task PlayComputerGame(CancellationToken ct)
        {
            try
            {
                while (isGameActive && !ct.IsCancellationRequested)
                {
                    // Move from "One"
                    currentPlayer = "One";
                    await MakeComputerMove();

                    await Task.Delay(300, ct);

                    // Move from "Two"
                    currentPlayer = "Two";
                    await MakeComputerMove();

                    await Task.Delay(500, ct);
                    // After second move, SendChoicesToServer() sees both moves, sends them.
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation if needed.
            }
        }

        /// <summary>
        /// Makes a single random move on behalf of the computer.
        /// </summary>
        private async Task MakeComputerMove()
        {
            await Task.Delay(500);
            while (true)
            {
                int computerChoice = random.Next(3);
                await MakeChoice(computerChoice);
                break;
            }
        }

        /// <summary>
        /// Ends the current game, displays the final result, and disables inputs.
        /// Also cancels the computer loop if running.
        /// </summary>
        /// <param name="message">A message describing the outcome of the game.</param>
        private void GameOver(string message)
        {
            isGameActive = false;
            ResultText.Text = message;
            foreach (var button in buttons)
            {
                button.IsEnabled = false;
            }

            if (computerGameCts != null)
            {
                computerGameCts.Cancel();
                computerGameCts.Dispose();
                computerGameCts = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Defines the possible game modes.
    /// </summary>
    public enum GameMode
    {
        /// <summary>
        /// Player vs Player mode: both moves are human-driven.
        /// </summary>
        PlayerVsPlayer,

        /// <summary>
        /// Player vs Computer mode: one move is human-driven, the other is computer-driven.
        /// </summary>
        PlayerVsComputer,

        /// <summary>
        /// Computer vs Computer mode: both moves are computer-driven.
        /// </summary>
        ComputerVsComputer
    }
}
