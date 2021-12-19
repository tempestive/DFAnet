using Spectre.Console;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace DFA
{
    [DataContract]
    public class MyStato : DFAState
    {
        [DataMember]
        public double v;
    }

    /// <summary>
    /// DFA
    /// 
    ///   [[START]] --> [FIRST_STEP] -+-if(valore is odd)---> [ODD] --+-> [LAST_STEP] --> [[FINISH]]
    ///                               |                               |
    ///                               |                               |
    ///                               +-if(valore is even)--> [EVEN] -+
    ///
    /// State Actions
    /// 
    ///   [[START]]    ==> nothing
    ///   [FIRST_STEP] ==> nothing
    ///   [ODD]        ==> print DISPARI
    ///   [EVEN]       ==> print PARI
    ///   [FIRST_STEP] ==> nothing
    ///   [[FINISH]]   ==> nothing
    ///                                
    /// </summary>
    public class MyMacchina : DFA<MyStato>
    {
        public int valore = 0;
        public int START = 0, FIRST_STEP = 1, IS_EVEN = 2, IS_ODD = 3, LAST_STEP = 4, FINISH = 5;

        public override void DefineStates()
        {
            AddState(START, new MyStato() { v = 10.0, Action = () => Log($"Inizio: sono nello stato {CurrentState.v}") });
            AddState(FIRST_STEP, new MyStato() { v = 10.1, Action = () => Log($"Primo passo: sono nello stato {CurrentState.Id}. valore: {valore}") });
            AddState(IS_EVEN, new MyStato()
            {
                v = 10.2,
                Action = () =>
                {
                    Log($"PARI: sono nello stato {CurrentState.Id}. valore: {valore}");
                    AnsiConsole.WriteLine($"Il numero {this.valore} è");
                    AnsiConsole.Render(new FigletText($"PARI").LeftAligned().Color(Color.Green));
                },
            });
            AddState(IS_ODD, new MyStato()
            {
                v = 10.3,
                Action = () =>
                {
                    Log($"DISPARI: sono nello stato {CurrentState.Id}. valore: {valore}");
                    AnsiConsole.WriteLine($"Il numero {this.valore} è");
                    AnsiConsole.Render(new FigletText($"DISPARI").LeftAligned().Color(Color.Green));
                },
            });
            AddState(LAST_STEP, new MyStato() { v = 10.3, Action = () => Log($"Ultimo passo: sono nello stato {CurrentState.Id}. valore: {valore}") });
            AddState(FINISH, new MyStato() { v = 10.5, Action = () => Log($"Finito: sono nello stato {CurrentState.Id}. valore: {valore}") });
        }

        public override void DefineTransitions()
        {
            AddTransitionLink(START, FIRST_STEP, () => true);
            AddTransitionLink(FIRST_STEP, IS_EVEN, () => valore % 2 == 0);
            AddTransitionLink(FIRST_STEP, IS_ODD, () => valore % 2 != 0);
            AddTransitionLink(IS_EVEN, FINISH, () => true);
            AddTransitionLink(IS_ODD, FINISH, () => true);
        }

        private static void Log(string logString)
        {
            AnsiConsole.MarkupLine($"[red]{logString}[/]");
        }

    }

    internal static class Program
    {
        static void Main(string[] args)
        {
            AnsiConsole.WriteLine("Hello DFA World!");

            string fileName = Path.GetTempFileName();
            AnsiConsole.MarkupLine($"Temporary file: {fileName}");

            {
                int? number = null; ;
                while (!number.HasValue)
                {
                    try
                    {
                        AnsiConsole.MarkupLine("[yellow]Write a number and press <ENTER>[/]");
                        string numberAsString = Console.ReadLine();
                        number = int.Parse(numberAsString);
                    }
                    catch (FormatException) { }
                }
                MyMacchina macchina = new MyMacchina();
                macchina.valore = number.Value;
                macchina.StartFrom(macchina.START);
                // go to next state
                macchina.Move();

                // save dfa
                using (FileStream outf = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
                {
                    macchina.Save(outf);
                }
                AnsiConsole.WriteLine("DFA saved");
            }
            {
                // reload the dfa (in a different empty variable)
                MyMacchina macchina2;
                using (FileStream inf = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    macchina2 = MyMacchina.Load<MyMacchina>(inf);
                }
                AnsiConsole.WriteLine("DFA loaded");
                while (macchina2.CurrentStateId != macchina2.FINISH)
                {
                    IEnumerable<int> nexts = macchina2.GetNextStates();
                    if (!nexts.Any())
                    {
                        throw new ApplicationException($"Bad designed DFA: there aren't any transition from {macchina2.CurrentState.Id}");
                    }
                    foreach (int next in nexts)
                    {
                        if (macchina2.CanMoveTo(next))
                        {
                            macchina2.MoveTo(next);
                            break;
                        }
                    }
                }
            }

            File.Delete(fileName);
        }
    }
}
