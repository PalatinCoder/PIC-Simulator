using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using System.Collections.ObjectModel;

namespace PIC_Simulator
{
    /// <summary>
    /// Parst die Textfiles und setzt sie in die jeweiligen Instructions um
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
            Collection<Instruction> programm = new Collection<Instruction>();

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
            /*
             * Index,Länge   Inhalt
             * 0,4           LfdNr Zeile mit Ops
             * 5,4           Op als Byte (hex)
             * 20,5          LfdNr
             * 27,9 oder *   Label
             * 36,*          Payload
             */


            return null;
        }
    }
}
