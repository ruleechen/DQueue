# DQueue
A message queue clients wrapper with parallel supported. Queue clients support Redis and RabbitMQ. You will no need to care about the parallel on receive or handler the message. The samples below will give you the first impression of this component.

Main Message Flow
------------
Each receive thread will keep on receiving message until the specified max parallel number or there is no more message for 1 second,  then start parallel execution. 
```text

             Productor           Server                                 Consumer
         |--------------|     |----------|     |------------------------------------------------------|
         |              |     |  queue1  |     |                      |----------| -> user handler -> |
send --> |  enqueue and | --> |  queue2  | --> | -> dequeue thread -> | parallel | -> user handler -> | --> done
         |  emit event  |     |  queuex  |     |                      |----------| -> user handler -> |
         |--------------|     |----------|     |------------------------------------------------------|
         
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

Updates 2016.09.01 (V1)
------------
1. New IgnoreHash rule (for KaiSheng's request)
2. Optimize task scheduler
3. Add timeout feature, add consumer.**OnTimeout** event
4. Rename consumer.**Complete** to consumer.**OnComplete**
5. Add timestamp to all messages, such as {"billno":"123456789",**"$EnqueueTime$":"2016-09-01 05:10:56"**}

Updates 2016.10.10 (V2)
------------
1. Completely new better thread architecture on consumer
2. Introduced .net framework built-in parallel functions
3. Introduced health monitor for consumers with Diagnose/Rescue/Report functions
4. Fix redis provider connection issue on some unexpected environment
5. Make many optimizations
