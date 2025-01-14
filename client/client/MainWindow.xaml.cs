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
    public partial class MainWindow : Window
    {
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

        // Опціонально (для прикладу): зберігатимемо список ходів
        private List<RoundInfo> roundHistory = new List<RoundInfo>();

        // Клас для зберігання запису одного раунду
        private class RoundInfo
        {
            public int RoundNumber { get; set; }
            public string PlayerOneMove { get; set; }
            public string PlayerTwoMove { get; set; }
            public string RoundResult { get; set; }
        }

        private DateTime lastMoveTime = DateTime.MinValue;
        private readonly TimeSpan moveDelay = TimeSpan.FromSeconds(0.5);

        public MainWindow()
        {
            InitializeComponent();
            InitializeGame();
            LoadConfiguration();
            RefreshPorts();
        }

        private void InitializeGame()
        {
            buttons = new Button[1, 3];
            random = new Random();
            CreateChoices();
        }

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

            // Label the three buttons
            buttons[0, 0].Content = "💎 Rock";
            buttons[0, 1].Content = "📄 Paper";
            buttons[0, 2].Content = "✂ Scissors";
        }

        private void SetGameBoardActive(bool isActive)
        {
            foreach (var button in buttons)
            {
                button.IsEnabled = isActive;
            }
        }

        private void RefreshPorts()
        {
            PortComboBox.Items.Clear();
            foreach (string port in SerialPort.GetPortNames())
            {
                PortComboBox.Items.Add(port);
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    // Read all lines into a dictionary
                    var lines = File.ReadAllLines(configPath);
                    var configDict = new Dictionary<string, string>();
                    foreach (var line in lines)
                    {
                        // Skip empty or malformed lines
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var val = parts[1].Trim();
                            configDict[key] = val;
                        }
                    }

                    // Now retrieve the settings we care about
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

                // Instead of <reset/>, send an INI-like one-liner:
                string response = await SendMessageWithTimeout("reset=1", 2000);
                // We still check for "game_reset" in the response
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

        private async void GameButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isGameActive || isComputerTurn || (DateTime.Now - lastMoveTime) < moveDelay) return;

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

        private async Task MakeChoice(int choice)
        {
            if (!isGameActive) return;

            if (currentPlayer == "One") currentChoicePlayerOne = choice;
            else if (currentPlayer == "Two") currentChoicePlayerTwo = choice;

            currentPlayer = currentPlayer == "One" ? "Two" : "One";
            StatusText.Text = $"Current player: {currentPlayer}";

            await SendChoicesToServer();
        }

        private async Task SendChoicesToServer()
        {
            if (currentChoicePlayerOne.HasValue && currentChoicePlayerTwo.HasValue)
            {
                // For example: choices=playerOne=0;playerTwo=2
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

        private void ProcessServerResponse(string response)
        {
            // We keep the same checks for "invalid_move", "error_parsing_xml", etc.
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

        private void ShowRoundMoves(string roundResult)
        {
            // Convert choice -> Rock/Paper/Scissors
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
                // ...
            }
        }

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

        private void PortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveConfiguration();
        }
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
    }

    public enum GameMode
    {
        PlayerVsPlayer,
        PlayerVsComputer,
        ComputerVsComputer
    }
}
