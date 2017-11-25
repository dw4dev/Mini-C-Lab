using System;
using System.IO;
using System.Threading;

public class CSharpLab
{
    public static void Test()
    {
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(500);
            Console.Write("Hello, World! ");
        }
        Console.WriteLine("\nDone!");
    }
}

