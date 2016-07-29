using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleHangman {
    public static class Extensions {
        // Extend char[] with a generic transform function.
        public static void Transform(this char[] target, Func<char, int, char> xform) {
            for (int i = 0; i < target.Length; i++) {
                target[i] = xform(target[i], i);
            }
        }

        // Extend char with the ToUpper() function.
        public static char ToUpper(this char c) {
            try {
                return c.ToString().ToUpper().ToCharArray()[0];
            } catch {
                throw;
            }
        }

    }

    class Program {
        private enum gameStates { ACTIVE, ROBOT, WON, LOST, ABORTED }

        static void Main(string[] args) {
            string[] words = System.IO.File.ReadAllLines("../../words.txt");

            hangmanRobot robot = new hangmanRobot(words);

            do {
                playGame(words, robot);
            } while (playAgain());
        }

        private static void playGame(string[] words, hangmanRobot robot) {
            hangmanGallows gallows = new hangmanGallows();

            Random RNG = new Random((int) System.DateTime.Now.Ticks);
            string word = words[RNG.Next(words.Length)].ToUpper();

            // Accumulate guessed letters in a string builder.
            StringBuilder guessed = new StringBuilder();

            // Track matched characters in a char[].
            // Unmatched letters are displayed as a hyphen.
            char[] match = new string('-', word.Length).ToCharArray();

            gameStates state = gameStates.ACTIVE;

            // main game-play loop
            updateDisplay(guessed, match, state, gallows);
            while (state == gameStates.ACTIVE || state == gameStates.ROBOT) {
                ConsoleKeyInfo ki;
                if (state == gameStates.ACTIVE){
                        ki = Console.ReadKey(true);
                }
                else{
                    ki = robot.nextKeyInfo();
                }

                char letter = ki.KeyChar.ToUpper();

                switch (ki.Key) {
                    case ConsoleKey.Escape:
                        state = gameStates.ABORTED;
                        break;

                    case ConsoleKey.F1:
                        state = gameStates.ROBOT;
                        robot.reset();
                        break;

                    default:
                        if (word.Contains(letter)) {
                            // When a letter matches, transform the match string to display
                            // the matched letter in all positions where it matches.
                            match.Transform((c, i) => {
                                if (word.ToCharArray()[i] == letter) {
                                    return letter;
                                }
                                else {
                                    return c;
                                }
                            });

                            // Have all chars in the word been matched?
                            if (!match.Contains('-')) {
                                state = gameStates.WON;
                            }

                        }
                        else {
                            guessed.Append(letter);
                            if (guessed.Length > gallows.Stages()) {
                                state = gameStates.LOST;
                            }
                            else if ((state == gameStates.ROBOT) && (guessed.Length > gallows.Stages() - 1)) {
                                // If robot is playing and has only one bad guess remaining, exit from robot mode 
                                // to allow user to make more informed guesses.
                                state = gameStates.ACTIVE;
                            }
                        }
                        break;
                }
                
                updateDisplay(guessed, match, state, gallows);
            } // main game-play loop
        }

        private static void updateDisplay(StringBuilder guessed, char[] match, gameStates state, hangmanGallows gallows) {
            Console.Clear();
            Console.WriteLine("\nGuessed: {0}", guessed.ToString());
            gallows.display(guessed.Length);
            Console.WriteLine("Word: {0}", new string(match));
            switch (state) {
                case gameStates.ACTIVE:
                    Console.Write("Type a letter to make a guess, \nF1 to let the robot attempt to solve it, \nor Esc to abandone this game:");
                    break;

                case gameStates.LOST:
                    Console.Write("You suck.");
                    break;

                case gameStates.WON:
                    Console.Write("You rock.");
                    break;

                case gameStates.ABORTED:
                    Console.Write("Quitter.");
                    break;
            }
        }

        private static bool playAgain() {
            Console.Write("\nWould you like to play another game?");
            return (Console.ReadKey().Key == ConsoleKey.Y);
        }
    }

    public class hangmanGallows {
        private static int[, ,] data = {
            {{0, ' '}, {0, ' '}, {3, '_'}, {3, '_'}, {3, '_'}, {0, ' '}, {0, ' '}},
            {{0, ' '}, {2, '|'}, {0, ' '}, {0, ' '}, {0, ' '}, {4, '|'}, {0, ' '}},
            {{0, ' '}, {2, '|'}, {0, ' '}, {0, ' '}, {0, ' '}, {5, 'O'}, {0, ' '}},
            {{0, ' '}, {2, '|'}, {0, ' '}, {0, ' '}, {7, '/'}, {6, '|'}, {8, '\\'}},
            {{0, ' '}, {2, '|'}, {0, ' '}, {0, ' '}, {0, ' '}, {6, '|'}, {0, ' '}},
            {{0, ' '}, {2, '|'}, {0, ' '}, {0, ' '}, {9, '/'}, {0, ' '}, {10,'\\'}},
            {{1, '_'}, {1, '|'}, {1, '_'}, {1, '_'}, {1, '_'}, {1, '_'}, {1, '_'}}
        };

        public int Stages() {
            return 9;
        }

        public void display(int stage) {
            for (int row = 0; row < 7; row++) {
                for (int col = 0; col < 7; col++) {
                    if (data[row, col, 0] <= stage) {
                        Console.Write((char) data[row, col, 1]);
                    } else {
                        Console.Write(" ");
                    }
                }
                Console.WriteLine();
            }
        }
    }


    public class hangmanRobot {
        private SortedDictionary<int, ConsoleKeyInfo> sortedHistogram = new SortedDictionary<int, ConsoleKeyInfo>();
        private int currentChar;

        public hangmanRobot(string[] words) {
            // One could use the well-known "ETAOINSHRDLU" list of most common letters in English,
            // but the list does not contain the entire alphabet nor does it reflect the slightly
            // altered distribution found in our passed-in word list.  Therefore, this constructor
            // creates a histogram to accuractly reflect letter-frequency in the passed-in word list.
            
            // First count the occurrence of each letter.
            const int LETTERS = 26;
            int[] histogram = new int[LETTERS];
            foreach (string word in words) {
                foreach (char letter in word.ToUpper().ToCharArray()) {
                    int i = letter - 'A';
                    if (i >= 0 && i < LETTERS) {
                        histogram[i]++;
                    }
                }
            }

            // Sort by count so that the robot can guess letters in the most-likely order.
            for (int i = 0; i < histogram.Length; i++) {
                sortedHistogram.Add(histogram[i], new ConsoleKeyInfo((char) (i + 'A'), ConsoleKey.A, false, false, false));
            }

            reset();
        }

        public void reset() {
            currentChar = 26;
        }

        public ConsoleKeyInfo nextKeyInfo() {
            // If there are remaining letters to guess, do so, else return ESCAPE (robot abandons game).
            if (currentChar > 0) {
                currentChar--;
                return sortedHistogram.ElementAt(currentChar).Value;
            } else {
                return new ConsoleKeyInfo((char) ConsoleKey.Escape, ConsoleKey.Escape, false, false, false);
            }
        }
    }
}
