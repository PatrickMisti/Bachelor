using Akka.Actor;

namespace SystemConsoleTests;

public class DemoActor : ReceiveActor
{
    public DemoActor()
    {
        Receive<string>(msg =>
        {
            Console.WriteLine($"DemoActor received message: {msg}");
        });
    }

    public static Props Prop()
    {
        return Props.Create(() => new DemoActor());
    }
}