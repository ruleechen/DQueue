# DQueue
A message queue clients wrapper with parallel supported. Queue clients support Redis and RabbitMQ. You will no need to care about the parallel on receive or handler the message. The samples below will give you the first impression of this component.

Main Message Flow
------------
Each receive threads will queue up one by one to get only one message form queue server
```text
                                                            |--> handler thread 1 -->|
message 1 -->|                    |--> receiver thread 1 -->|                        |--> complete 1
             |                    |                         |--> handler thread 2 -->|
             |--> queue server -->|
             |                    |                         |--> handler thread 3 -->|
message 2 -->|                    |--> receiver thread 2 -->|                        |--> complete 2
                                                            |--> handler thread 4 -->|
```

Sample Configuration
------------
```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="QueueProvider" value="Redis" />
    <add key="ConsumerTimeout" value="60" /><!--60 seconds, or with the timespan format: 00:01:00-->
  </appSettings>
  <connectionStrings>
    <add name="Redis_Connection" connectionString="127.0.0.1:6379,password=,allowAdmin=true" />
    <add name="RabbitMQ_Connection" connectionString="HostName=localhost,UserName=rulee,Password=abc123" />
  </connectionStrings>
</configuration>
```

Sample Message
------------
```c#
public class SampleMessage : IQueueMessage
{
  public string QueueName
  {
    get { return "TestQueue"; }
  }

  public string Text { get; set; }
}
```

Sample Producer
------------
```c#
var producer = new QueueProducer();
// any message type is allowed
producer.Send(new SampleMessage { Text = "test" });
```

Sample Consumer
------------
```c#
// specify 2 threads on receiving "SampleMessage"
var consumer = new QueueConsumer<SampleMessage>(2);

consumer.Receive((context) =>
{
  // handler thread 1
  Console.WriteLine(context.Message.Text); // "test"
});

consumer.Receive((context) =>
{
  // handler thread 2
  Console.WriteLine(context.Message.Text); // "test"
});

consumer.OnComplete((context) =>
{
  // will goes here when "handler thread 1" and "handler thread 2" are done
  
  foreach (var ex in context.Exceptions)
  {
    Console.WriteLine(ex.Message);
  }
});

consumer.OnTimeout((context) =>
{
  // will goes here when "handler thread 1" and "handler thread 2" are timeout
  // we can here to send notification email or enqueue again.
  
  SendEmail(context.Message);
});
```

Updates 2016.09.01
------------
1. new IgnoreHash rule (for KaiSheng's request)
2. add timeout feature
3. optimize task scheduler
4. rename consumer.**Complete** to consumer.**OnComplete**
5. add timestamp to all messages, such as {"billno":"123456789",**"$EnqueueTime$":"2016-09-01 05:10:56"**}
