using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace PIC_Simulator
{
    /// <summary>
    /// Ein einfacher Parser mit Regex. Es wird unterschieden zwischen Zeilen, die einen
    /// Opcode enthalten, und dem Rest. Ist ein Opcode vorhanden, wird eine Instanz der
    /// ProcessorInstruction mit den entsprechenden Werten aus dem Regex Match erstellt    /// 
    /// </summary>
    class ListingFileParser
    {
        /// <summary>
        /// Parst das angegebene Textfile
        /// </summary>
        /// <param name="file">fileobjekt</param>
        /// <param name="lineParsedCallback">Callback wenn eine Zeile geparst wurde</param>
        /// <returns>Collection von Instructions</returns>
        public static async Task Parse(StorageFile file, Action<Instruction> lineParsedCallback)
        {
            IList<String> listing = await FileIO.ReadLinesAsync(file);

            foreach (String line in listing)
            {
                Instruction instruction = ParseLine(line);
                if (instruction != null) lineParsedCallback(instruction);
            }
        }

        /// <summary>
        /// Wandelt eine Zeile aus dem Listing in eine Instruction um
        /// </summary>
        /// <param name="line">Quellcodezeile</param>
        /// <returns>Instruction|null</returns>
        private static Instruction ParseLine(String line)
        {
            // Pattern für Zeilen die Opcodes enthalten: 
            // Adresse | Opcode | Zeilennummer | Rest
            Regex ProcessorInstructionPattern = new Regex("^([0-9]{4}) ([0-9A-F]{4}) * ([0-9]{5}) (.*)$");
            Match m = ProcessorInstructionPattern.Match(line);
            if (m.Success)
            // Opcode gefunden
                return new ProcessorInstruction(int.Parse(m.Groups[3].Value), m.Groups[4].Value, ushort.Parse(m.Groups[2].Value, System.Globalization.NumberStyles.HexNumber));

            Regex InstructionPattern = new Regex("^ * ([0-9]{5}) (.*)$");
            Match i = InstructionPattern.Match(line);
            // alles andere
            return new Instruction(int.Parse(i.Groups[1].Value), i.Groups[2].Value);
        }
    }
}
