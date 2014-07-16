using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Globalization;

namespace AmazingDuinoInterface
{
    class Program
    {
        enum MessageType
        {
            Standard,
            Prompt,
            SerialLog,
            Error
        }

        class ProgramMustExitException : Exception { }

        static SerialPort arduinoInterface;
        static Boolean isBinary;

        private static void UseDefaultEncoding(out Encoding encode)
        {
            encode = Encoding.ASCII;
            MyConsole.WriteLine("Defaulting encoding to be {0}", Encoding.ASCII.WebName);
        }

        static void Main(string[] args)
        {
            Console.WindowWidth = Math.Min(Console.LargestWindowWidth, (int)(Console.WindowWidth * 1.5));
            Console.WindowHeight = Math.Min(Console.LargestWindowHeight, (int)(Console.WindowHeight * 1.5));

            MyConsole.WriteLine("Please input serial port name (leave blank for default):");
            String intName = MyConsole.ReadLine();
            if (intName.Trim().Length == 0)
            {
                intName = "COM4";
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
                    if (encodingName.Trim().Equals("binary", StringComparison.OrdinalIgnoreCase))
                    {
                        encoding = null;
                        isBinary = true;
                    }
                    else
                    {
                        encoding = Encoding.GetEncoding(encodingName);
                        if (encoding == null)
                        {
                            UseDefaultEncoding(out encoding);
                        }
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
                arduinoInterface.Encoding = encoding == null ? Encoding.ASCII : encoding;
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
            if (!isBinary)
            {
                ConsoleCommands.Add("literal", Literal);
                ConsoleCommandDescriptions.Add("/literal", "Sends a literal string, no comamnds or newlines, to the serial port.");
            }
            //ConsoleCommands.Add("incomingdisplay", InputMessageToggle);
            //ConsoleCommandDescriptions.Add("/incomingdisplay [state]", "Toggles whether incoming serial port messages are displayed.");
            ConsoleCommands.Add("input", InputConsoleStateManager);
            ConsoleCommandDescriptions.Add("/input <view|clear|amount>", "Display or clear the serial input buffer.");
            if (isBinary)
            {
                ConsoleCommands.Add("byte2hex", ByteConversionCommand);
                ConsoleCommandDescriptions.Add("/byte2hex <byte>", "Converts a decimal byte value to hexadecimal.");
                ConsoleCommands.Add("char2hex", CharConversionCommand);
                ConsoleCommandDescriptions.Add("/char2hex <char>", "Converts a character value to its byte representation.");
                ConsoleCommands.Add("hex2byte", HexConversionCommand);
                ConsoleCommandDescriptions.Add("/hex2byte <hex>", "Converts a hexadecimal byte value to its decimal representation.");
            }
            ConsoleCommands.Add("exit", () => { throw new ProgramMustExitException(); });
            ConsoleCommandDescriptions.Add("/exit", "Exits the program.");


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
                    if (isBinary)
                    {
                        byte[] parsedArray = null;
                        String binaryString = serialCommand.Replace(" 0x", "").Replace("0x", "");
                        if (binaryString.Length % 2 != 0)
                        {
                            WriteLine(MessageType.Error, "Error parsing hexadecimal string.");
                        }
                        else
                        {
                            parsedArray = new byte[binaryString.Length / 2];
                            for (int i = 0; i < binaryString.Length; i += 2)
                            {
                                try
                                {
                                    parsedArray[i / 2] = byte.Parse(binaryString.Substring(i, 2), NumberStyles.AllowHexSpecifier);
                                }
                                catch
                                {
                                    WriteLine(MessageType.Error, "Error parsing hexadecimal string.");
                                    parsedArray = null;
                                    break;
                                }
                            }
                        }

                        if (parsedArray != null)
                        {
                            WriteLine(MessageType.SerialLog, String.Format("Writing {0} byte(s) of binary data to serial port...", parsedArray.Length));
                            arduinoInterface.Write(parsedArray, 0, parsedArray.Length);
                        }
                    }
                    else
                    {
                        WriteLine(MessageType.SerialLog, "Sending message to port...");
                        arduinoInterface.WriteLine(serialCommand);
                    }
                }
            }
        }

        static string CurrentCommandArguments;

        static StringBuilder receivedData = new StringBuilder();

        static object synclock = new object();

        static void HexConversionCommand()
        {
            byte value;
            try
            {
                if (!CurrentCommandArguments.StartsWith("0x") && !CurrentCommandArguments.StartsWith("&h"))
                {
                    throw new Exception();
                }
                value = byte.Parse(CurrentCommandArguments.Substring(2), NumberStyles.AllowHexSpecifier);
            }
            catch
            {
                MyConsole.WriteLine("Must specify hexadecimal value to convert.");
                return;
            }

            MyConsole.WriteLine("Hexadecimal value {0} represents byte with decimal value {1}.", CurrentCommandArguments, value);
            char charVal = (char)value;
            if (char.IsLetterOrDigit(charVal) || char.IsPunctuation(charVal) || char.IsWhiteSpace(charVal))
            {
                MyConsole.WriteLine("Hexadecimal value {0} represents character literal '{1}'.", CurrentCommandArguments, charVal);
            }
        }

        static void ByteConversionCommand()
        {
            byte value;

            if (!byte.TryParse(CurrentCommandArguments, out value))
            {
                MyConsole.WriteLine("Must specify decimal byte value to convert.");
                return;
            }

            MyConsole.WriteLine("Hexadecimal value of {0} is 0x{0:X2}.", value);
        }

        static void CharConversionCommand()
        {
            char value;

            if (!char.TryParse(CurrentCommandArguments, out value))
            {
                MyConsole.WriteLine("Must specify character to convert.");
                return;
            }

            MyConsole.WriteLine("Byte decimal value of character literal '{0}' is {1}, or 0x{1:X2} in hexadecimal.", value, (byte)value);
        }

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
                                Console.Write(receivedData.ToString().Trim());
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
                        MyConsole.WriteLine("Buffer contains {0} byte(s) of received data.", isBinary ? receivedData.Length / 5 /* Each byte representation uses 5 characters: "0x<hex hex> " */ : receivedData.Length);
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
                if (isBinary)
                {
                    byte[] data = new byte[bytesToRead];
                    arduinoInterface.Read(data, 0, bytesToRead);
                    //+ BitConverter.ToString(data).Replace("-", " 0x"));
                    foreach (byte datVal in data)
                    {
                        receivedData.AppendFormat(" 0x{0:X2}", datVal);
                    }
                }
                else
                {
                    char[] data = new char[bytesToRead];
                    arduinoInterface.Read(data, 0, bytesToRead);
                    receivedData.Append(data);
                }
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
            WriteLine(MessageType.Prompt, "Please input literal message to be sent to serial port. The message will be sent without newline, and will be sent with textual encoding.");
            MyConsole.ForegroundColor = ConsoleColor.Red;
            String input = MyConsole.ReadLine();
            MyConsole.ForegroundColor = ConsoleColor.Gray;
            WriteLine(MessageType.SerialLog, "Sending literal textual message to serial port without newline...");
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
                case MessageType.Error:
                    break;
                //case MessageType.IncomingMessage:
                //    if (!DisplayIncomingMessages)
                //    {
                //        return;
                //    }

                //    MyConsole.ForegroundColor = ConsoleColor.DarkGray;
                //    MyConsole.Write("[Incoming Message] ");
                //    break;
            }

            MyConsole.ForegroundColor = type == MessageType.Error ? ConsoleColor.DarkRed : ConsoleColor.Gray;
            MyConsole.WriteLine(message);
            MyConsole.ForegroundColor = ConsoleColor.Red;
        }
    }
}
