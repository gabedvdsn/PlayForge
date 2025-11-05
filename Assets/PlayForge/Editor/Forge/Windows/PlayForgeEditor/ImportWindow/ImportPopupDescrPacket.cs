using System.Collections.Generic;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class ImportPopupDescrPacket
    {
        public readonly EValidationCode Code;
        public readonly List<string> Messages;

        public ImportPopupDescrPacket(EValidationCode code, List<string> messages)
        {
            Code = code;
            Messages = messages;
        }
    }
}
