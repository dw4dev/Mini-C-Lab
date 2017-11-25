Imports System
Imports System.IO
Imports System.Threading

Public Class CSharpLab
    Public Shared Sub Test()
        For i As Integer = 0 To 19
            Thread.Sleep(500)
            Console.Write("Hello, World! ")
        Next
        Console.WriteLine()
        Console.WriteLine("Done!")
    End Sub
End Class