using System;using System.IO;class EditorProbe{[STAThread]static void Main(string[] args){File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"received.txt"),args);}}
