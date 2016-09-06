using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ProgramBytesPushbullet
{
    public class pushData
    {
        public string body, title, type;
        public double modified;
        public bool dismissed;
        public pushData(string title, string body, string type)
        {
            this.title = title; this.body = body; this.type = type;
        }
    }
}

