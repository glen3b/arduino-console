using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace AmazingDuinoInterface
{
    class Program
    {
        enum MessageType
        {
            Standard,
            Prompt,
            SerialLog,
            IncomingMessage
        }

        class ProgramMustExitException : Exception { }

        static SerialPort arduinoInterface;

        private static void UseDefaultEncoding(out Encoding encode)
        {
            encode = Encoding.ASCII;
            MyConsole.WriteLine("Defaulting encoding to be {0}", Encoding.ASCII.WebName);
        }

        static void Main(string[] args)
        {
            MyConsole.WriteLine("Please input serial port name (leave blank for default):");
            String intName = MyConsole.ReadLine();
            if (intName.Trim().Length == 0)
            {
                intName = "COM3";
                MyConsole.WriteLine("Defaulting serial port to be {0}", intName);
            }

            MyConsole.WriteLine("Please input baud rate (leave blank for default):");
            int baudRate;
            if (!int.TryParse(MyConsole.ReadLine(), out baudRate))
            {
                baudRate = 9600;
                MyConsole.WriteLine("Defaulting baud rate to be {0}", baudRate);
            }

            MyConsole.WriteLine("Please input text encoding (leave blank for default):");
            Encoding encoding = null;
            String encodingName = MyConsole.ReadLine();
            if (encodingName != null && encodingName.Trim().Length > 0)
            {
                try
                {
                    encoding = Encoding.GetEncoding(encodingName);
                    if (encoding == null)
                    {
                        UseDefaultEncoding(out encoding);
                    }
                }
                catch
                {
                    UseDefaultEncoding(out encoding);
                }
            }
            else
            {
                UseDefaultEncoding(out encoding);
            }

            try
            {
                arduinoInterface = new SerialPort(intName, baudRate);
                arduinoInterface.Encoding = encoding;
                WriteLine(MessageType.SerialLog, "Opening serial port for communication...");
                arduinoInterface.Open();
            }
            catch (Exception except)
            {
                WriteLine(MessageType.SerialLog, "Error preparing serial port for communication, aborting program:");
                MyConsole.WriteLine(except.ToString());
                MyConsole.ReadKey(true);
                return;
            }

            MyConsole.ForegroundColor = ConsoleColor.DarkMagenta;
            MyConsole.WriteLine("Serial communication prompt is ready, please press enter to continue.");
            MyConsole.ReadKey(true);
            MyConsole.Clear();

            ConsoleCommands.Add("help", Help);
            ConsoleCommandDescriptions.Add("/help", "Displays information for all available commands.");
            ConsoleCommands.Add("literal", Literal);
            ConsoleCommandDescriptions.Add("/literal", "Sends a literal string, no comamnds or newlines, to the serial port.");
            ConsoleCommands.Add("exit", () => { throw new ProgramMustExitException(); });
            ConsoleCommandDescriptions.Add("/exit", "Exits the program.");
            //ConsoleCommands.Add("incomingdisplay", InputMessageToggle);
            //ConsoleCommandDescriptions.Add("/incomingdisplay [state]", "Toggles whether incoming serial port messages are displayed.");
            ConsoleCommands.Add("input", InputConsoleStateManager);
            ConsoleCommandDescriptions.Add("/input <view|clear|amount>", "Display or clear the serial input buffer.");


            arduinoInterface.DataReceived += new SerialDataReceivedEventHandler(arduinoInterface_DataReceived);

            MyConsole.ForegroundColor = ConsoleColor.Yellow;
            MyConsole.WriteLine("Welcome to the serial communication prompt. Type /help for help.");
            MyConsole.ForegroundColor = ConsoleColor.Gray;

            while (true)
            {
                MyConsole.ForegroundColor = ConsoleColor.Red;
                string serialCommand = MyConsole.ReadLine();
                MyConsole.ForegroundColor = ConsoleColor.Gray;
                if (serialCommand.StartsWith("/"))
                {
                    string[] splitStrings = serialCommand.Substring(1).Split(new char[] { ' ' }, 2);
                    serialCommand = splitStrings[0];
                    CurrentCommandArguments = splitStrings.Length > 1 ? splitStrings[1] : null;

                    Action cmd;
                    if (!ConsoleCommands.TryGetValue(serialCommand, out cmd))
                    {
                        WriteLine(MessageType.Standard, "Unknown command. Type /help for help.");
                    }
                    else
                    {
                        try
                        {
                            cmd();
                        }
                        catch (ProgramMustExitException)
                        {
                            arduinoInterface.Close();
                            return;
                        }
                    }
                }
                else
                {
                    WriteLine(MessageType.SerialLog, "Sending message to port...");
                    arduinoInterface.WriteLine(serialCommand);
                }
            }
        }

        static string CurrentCommandArguments;

        static StringBuilder receivedData = new StringBuilder();

        static object synclock = new object();



        static void InputConsoleStateManager()
        {

            switch (CurrentCommandArguments == null ? null : CurrentCommandArguments.Trim().ToUpperInvariant())
            {
                case "VIEW":
                    MyConsole.ClearDisplay();
                    int dispLen = -1;
                    while (!Console.KeyAvailable)
                    {
                        lock (synclock)
                        {
                            if (receivedData.Length != dispLen)
                            {
                                Console.Clear();
                                Console.Write(receivedData.ToString());
                                dispLen = receivedData.Length;
                            }
                        }
                    }
                    Console.ReadKey(true); // Prevent sending blank message
                    MyConsole.RestoreDisplay();

                    break;
                case "CLEAR":
                    lock (synclock)
                    {
                        receivedData.Clear();
                        MyConsole.WriteLine("Input buffer cleared.");
                    }
                    break;
                case "AMOUNT":
                    lock (synclock)
                    {
                        MyConsole.WriteLine("Buffer contains {0} byte(s) of received data.", receivedData.Length);
                    }
                    break;
                default:
                    MyConsole.WriteLine("Must specify action: VIEW, CLEAR, or AMOUNT.");
                    return;
            }
        }

        static void arduinoInterface_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (synclock)
            {
                int bytesToRead = arduinoInterface.BytesToRead;
                char[] data = new char[bytesToRead];
                arduinoInterface.Read(data, 0, bytesToRead);
                receivedData.Append(data);
            }

            //lock (synclock)
            //{
            //    int bytesToRead = arduinoInterface.BytesToRead;
            //    char[] data = new char[bytesToRead];
            //    arduinoInterface.Read(data, 0, bytesToRead);
            //    string dataStr = new string(data);

            //    while (true)
            //    {
            //        int newlinePosition = dataStr.IndexOf("\r\n");
            //        if (newlinePosition < 0)
            //        {
            //            receivedData.Append(dataStr);
            //            return;
            //        }
            //        //else if (newlinePosition == dataStr.Length - 2 /* End of string */)
            //        //{
            //        //    receivedData.Append(dataStr, 0, dataStr.Length - 2);
            //        //    return;
            //        //}
            //        else
            //        {
            //            receivedData.Append(dataStr, 0, newlinePosition);
            //            string displayMessage = receivedData.ToString();
            //            WriteLine(MessageType.IncomingMessage, displayMessage);
            //            receivedData.Clear();
            //            dataStr = newlinePosition + 2 >= dataStr.Length ? "" : dataStr.Substring(newlinePosition + 2);
            //        }
            //    }
            //}
        }

        static void Help()
        {
            MyConsole.ForegroundColor = ConsoleColor.Green;
            MyConsole.WriteLine("Available commands:");
            foreach (var command in ConsoleCommandDescriptions)
            {
                MyConsole.ForegroundColor = ConsoleColor.Magenta;
                MyConsole.Write(command.Key);
                MyConsole.ForegroundColor = ConsoleColor.Cyan;
                MyConsole.Write(" - ");
                MyConsole.ForegroundColor = ConsoleColor.DarkYellow;
                MyConsole.WriteLine(command.Value);
            }
            MyConsole.ForegroundColor = ConsoleColor.Gray;
        }

        static bool DisplayIncomingMessages = true;

        [Obsolete]
        static void InputMessageToggle()
        {
            if (CurrentCommandArguments == null)
            {
                MyConsole.WriteLine("Current incoming message display state: {0}", DisplayIncomingMessages ? "ON" : "OFF");
                //WriteLine(MessageType.Standard, "Must supply incoming message display state.");
                return;
            }
            switch (CurrentCommandArguments.ToUpperInvariant())
            {
                case "TRUE":
                case "YES":
                case "ON":
                    DisplayIncomingMessages = true;
                    break;
                case "FALSE":
                case "NO":
                case "OFF":
                    DisplayIncomingMessages = false;
                    break;
                default:
                    MyConsole.WriteLine("Invalid state description.");
                    return;

            }

            MyConsole.WriteLine("Turned display of incoming messages {0}.", DisplayIncomingMessages ? "on" : "off");
        }

        static void Literal()
        {
            WriteLine(MessageType.Prompt, "Please input literal message to be sent to serial port.");
            MyConsole.ForegroundColor = ConsoleColor.Red;
            String input = MyConsole.ReadLine();
            MyConsole.ForegroundColor = ConsoleColor.Gray;
            WriteLine(MessageType.SerialLog, "Sending literal message to serial port without newline...");
            arduinoInterface.Write(input);
        }

        static Dictionary<String, Action> ConsoleCommands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        static Dictionary<String, String> ConsoleCommandDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static void WriteLine(MessageType type, string message)
        {
            switch (type)
            {
                case MessageType.Prompt:
                case MessageType.Standard:
                    break;
                case MessageType.SerialLog:
                    MyConsole.ForegroundColor = ConsoleColor.DarkGray;
                    MyConsole.Write("[Serial] ");
                    break;
                case MessageType.IncomingMessage:
                    if (!DisplayIncomingMessages)
                    {
                        return;
                    }

                    MyConsole.ForegroundColor = ConsoleColor.DarkGray;
                    MyConsole.Write("[Incoming Message] ");
                    break;
            }

            MyConsole.ForegroundColor = ConsoleColor.Gray;
            MyConsole.WriteLine(message);
            MyConsole.ForegroundColor = ConsoleColor.Red;
        }
    }
}
