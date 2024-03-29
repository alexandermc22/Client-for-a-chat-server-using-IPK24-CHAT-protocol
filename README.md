# IPK Project 1: Client for a chat server using IPK24-CHAT protocol
# Author: Alexandr Tihanschi xtihan00

### Structure
- Project basis
- Project functionality description
- Description of points of contention in the code
- Testing
- Bibliography

### Project basis

The project is based on 3 main classes 
1) Program.cs from where methods for tcp or udp are called
2) TcpCommunicaton.cs, UdpCommunicaton.cs where methods for Client's work via tcp or udp are implemented.

When working with tcp and udp, classes inherited from the IMessage interface are used, each of which implements methods to translate a message into tcp/udp form or vice versa

The Options.cs class was also implemented for convenient handling of input arguments

### Project functionality description
When working with tcp/udp, asynchronous processes were used. These processes commutate with each other using shared variables and mutexes, the two main methods are 
- Receiving a message 
- Sending a message

 
Receiving a message : 
Serves to receive messages from the server, issue it to the user and send an acknowledgement in case of udp, can also terminate the process when bye is received from the server
 
Sending a message:
Reads data from the user, then checks it for validity and sends it to the server, depending on the result it can switch to another state or terminate (e.g. if no confirmation was received from the server).

In order not to overload the code we implemented helper methods for sending and waiting for confirmation

 ### Description of points of contention in the code

 1) Great similarity in the structure of udp,tcp classes
    Both classes have very similar logic and implementation and if I wanted to, I could combine them into 1 class, but I decided that it would degrade the readability of the code too much and make it too confusing
 2) Recursion of ReceiveMessage() method
    When working with udp I encountered a problem that when receiving an error from the server, the ReceiveMessage() method should send a bye message for correct termination, but since ReceiveMessage() waits for confirmation from the server, it must be recursively called to receive the confirmation.
 3) Shutdown even if no response to the Bye message is received
    The task says that the correct termination of work over udp is the confirmed bye message, but in case the server stops working or there is some error with the client connection, I think that it is impossible to let the program wait indefinitely for confirmation instead the confirmation bye will be waited as long as the other confirmations (MaxRetries, UdpTimeout).
 4) Waiting for Reply at tcp
    A similar problem can occur when waiting for Reply on tcp, to be safe the client will wait for the message for a total of 2 seconds, and then it will terminate the connection with the server.
 5) Using Environment.Exit(0)
When completing the connection through the ReceiveMessages() method, I encountered a problem that the Console.ReadLine() command blocks the main thread and await Task.WhenAny() does not complete when ReceiveMessages() ends via return, therefore, when the program terminates, it was necessary to read something from the input Therefore, I had to forcefully terminate the program via Environment.Exit(0)

### Testing
- The main operation of the code and various user inputs were tested to verify that the project works properly and there will be no unhandled exceptions
- Testing was carried out
    - at the beginning using debugging
    - then using a traffic tracking application WireShark
- Testing was carried out on a virtual machine where only the basic tools provided in the task were installed
- Next will be the result of testing via WireShark
    ##### TCP
    Input and Output: 
    >/auth wrongid wrongsecret name
    
    >Failure: Authentication failed - Provided user secret is not valid.
    
    >/auth xtihan00 00d3554d-30c3-4a47-xxx name123
    
    >Success: Authentication successful.
    
    >Server: name123 joined discord.general.
    
    >/rename
    
    >ERR: Wrong input, repeat
    
    >/rename loooooooooooooooooooooooooooooooooong
    
    >ERR: Wrong input, repeat
    
    >Server: najtom joined discord.general.
    
    >/rename name111
    
    >Server: anop joined discord.general.
    
    >Server: anop left discord.general.
    
    >test
    
    >/join loooooooooooooooooooooooooooooooooong
    
    >ERR: System.ArgumentException: Channel ID and Display Name cannot exceed 20 characters in length.
        at Client.Messeges.Join.ToTcpString(Join join) in /home/ipk/ipk/Client/Messeges/Join.cs:line 17
        at Client.TcpCommunication.SendMessages(TcpClient tcpClient) in /home/ipk/ipk/Client/TcpCommunication.cs:line 182
    
    >/join discord.verified-1
    
    >Server: name111 joined discord.verified-1.
    
    >Success: Channel discord.verified-1 successfully joined.
    
    >test
    
    >end
    
    WireShark logs:
    >No.     Time           Source                Destination           Protocol Length Info
    206 5.296091       147.229.208.84        147.229.8.244         IPK24-CHAT 94     C → Server | AUTH wrongid AS name USING wrongsecret
    
    >No.     Time           Source                Destination           Protocol Length Info
        208 5.297677       147.229.8.244         147.229.208.84        IPK24-CHAT 127    Server → C | REPLY NOK IS Authentication failed - Provided user secret is not valid.
    
    >No.     Time           Source                Destination           Protocol Length Info
        639 20.180276      147.229.208.84        147.229.8.244         IPK24-CHAT 123    C → Server | AUTH xtihan00 AS name123 USING 00d3554d-30c3-4a47-a37e-xxx
    
    >No.     Time           Source                Destination           Protocol Length Info
        640 20.185656      147.229.8.244         147.229.208.84        IPK24-CHAT 94     Server → C | REPLY OK IS Authentication successful.
    
    >No.     Time           Source                Destination           Protocol Length Info
        655 20.439477      147.229.8.244         147.229.208.84        IPK24-CHAT 106    Server → C | MSG FROM Server IS name123 joined discord.general.
    
    >No.     Time           Source                Destination           Protocol Length Info
    1197 34.848926      147.229.8.244         147.229.208.84        IPK24-CHAT 105    Server → C | MSG FROM Server IS najtom joined discord.general.
    
    >No.     Time           Source                Destination           Protocol Length Info
    1364 39.466473      147.229.8.244         147.229.208.84        IPK24-CHAT 103    Server → C | MSG FROM Server IS anop joined discord.general.
    
    >No.     Time           Source                Destination           Protocol Length Info
    1375 39.721769      147.229.8.244         147.229.208.84        IPK24-CHAT 101    Server → C | MSG FROM Server IS anop left discord.general.
    
    >No.     Time           Source                Destination           Protocol Length Info
    1443 41.200494      147.229.208.84        147.229.8.244         IPK24-CHAT 80     C → Server | MSG FROM name111 IS test
    
    >No.     Time           Source                Destination           Protocol Length Info
    2154 59.762752      147.229.208.84        147.229.8.244         IPK24-CHAT 90     C → Server | JOIN discord.verified-1 AS name111
    
    >No.     Time           Source                Destination           Protocol Length Info
    2177 60.329602      147.229.8.244         147.229.208.84        IPK24-CHAT 109    Server → C | MSG FROM Server IS name111 joined discord.verified-1.
    
    >No.     Time           Source                Destination           Protocol Length Info
    2186 60.370727      147.229.8.244         147.229.208.84        IPK24-CHAT 115    Server → C | REPLY OK IS Channel discord.verified-1 successfully joined.
    
    >No.     Time           Source                Destination           Protocol Length Info
    2383 66.349376      147.229.208.84        147.229.8.244         IPK24-CHAT 80     C → Server | MSG FROM name111 IS test
    
    >No.     Time           Source                Destination           Protocol Length Info
    2611 71.721191      147.229.208.84        147.229.8.244         IPK24-CHAT 79     C → Server | MSG FROM name111 IS end
    
    >No.     Time           Source                Destination           Protocol Length Info
    2664 73.529320      147.229.208.84        147.229.8.244         IPK24-CHAT 59     C → Server | BYE

   ##### UDP
   Input and Output:
    >/auth wrongid wrongsecret name
    
    >Failure: Authentication failed - Provided user secret is not valid.
    
    >/auth xtihan00 00d3554d-30c3-4a47-a37e-xxx name123
    
    >Success: Authentication successful.
    
    >Server: name123 joined discord.general.
    
    >/rename
    
    >ERR: Wrong input, repeat
    
    >/rename name111
    
    >Server: mlem left discord.general.
    
    >test
    
    >/join
    
    >ERR: Wrong input, repeat
    
    >/join discord.verified-1
    
    >Server: name111 joined discord.verified-1.
    
    >Success: Channel discord.verified-1 successfully joined.
    
    >Server: anop joined discord.verified-1.
    
    >bye

    WireShark logs:
    >No.     Time           Source                Destination           Protocol Length Info
    212 5.641973       147.229.208.84        147.229.8.244         IPK24-CHAT 70     C → Server | ID=0, Type=auth, UserName=wrongid, DisplayName=name, Secret=wrongsecret

    >No.     Time           Source                Destination           Protocol Length Info
        213 5.642641       147.229.8.244         147.229.208.84        IPK24-CHAT 60     Server → C | Type=confirm, RefID=0

    >No.     Time           Source                Destination           Protocol Length Info
        214 5.643267       147.229.8.244         147.229.208.84        IPK24-CHAT 107    Server → C | ID=0, Type=reply, Result=NOK, RefID=0, Content=Authentication failed - Provided user secret is not valid.
    
    >No.     Time           Source                Destination           Protocol Length Info
        215 5.646127       147.229.208.84        147.229.8.244         IPK24-CHAT 45     C → Server | Type=confirm, RefID=0
    
    >No.     Time           Source                Destination           Protocol Length Info
        492 14.974654      147.229.208.84        147.229.8.244         IPK24-CHAT 99     C → Server | ID=1, Type=auth, UserName=xtihan00, DisplayName=name123, Secret=00d3554d-30c3-4a47-a37e-xxx
    
    >No.     Time           Source                Destination           Protocol Length Info
        493 14.975372      147.229.8.244         147.229.208.84        IPK24-CHAT 60     Server → C | Type=confirm, RefID=1
    
    >No.     Time           Source                Destination           Protocol Length Info
        494 14.978742      147.229.8.244         147.229.208.84        IPK24-CHAT 75     Server → C | ID=256, Type=reply, Result=OK, RefID=1, Content=Authentication successful.
    
    >No.     Time           Source                Destination           Protocol Length Info
        495 14.979516      147.229.208.84        147.229.8.244         IPK24-CHAT 45     C → Server | Type=confirm, RefID=256
    
    >No.     Time           Source                Destination           Protocol Length Info
        533 16.359125      147.229.8.244         147.229.208.84        IPK24-CHAT 84     Server → C | ID=512, Type=msg, DisplayName=Server, Content=name123 joined discord.general.
    
    >No.     Time           Source                Destination           Protocol Length Info
        534 16.360481      147.229.208.84        147.229.8.244         IPK24-CHAT 45     C → Server | Type=confirm, RefID=512
    
    >No.     Time           Source                Destination           Protocol Length Info
        763 23.944211      147.229.8.244         147.229.208.84        IPK24-CHAT 79     Server → C | ID=768, Type=msg, DisplayName=Server, Content=mlem left discord.general.
    
    >No.     Time           Source                Destination           Protocol Length Info
        764 23.945116      147.229.208.84        147.229.8.244         IPK24-CHAT 45     C → Server | Type=confirm, RefID=768
    
    >No.     Time           Source                Destination           Protocol Length Info
        837 26.260076      147.229.208.84        147.229.8.244         IPK24-CHAT 58     C → Server | ID=2, Type=msg, DisplayName=name111, Content=test
    
    >No.     Time           Source                Destination           Protocol Length Info
        838 26.261013      147.229.8.244         147.229.208.84        IPK24-CHAT 60     Server → C | Type=confirm, RefID=2
    
    >No.     Time           Source                Destination           Protocol Length Info
    1592 46.211521      147.229.208.84        147.229.8.244         IPK24-CHAT 72     C → Server | ID=3, Type=join, ChannelId=discord.verified-1, DisplayName=name111
    
    >No.     Time           Source                Destination           Protocol Length Info
    1593 46.212211      147.229.8.244         147.229.208.84        IPK24-CHAT 60     Server → C | Type=confirm, RefID=3
    
    >No.     Time           Source                Destination           Protocol Length Info
    1666 46.938096      147.229.8.244         147.229.208.84        IPK24-CHAT 87     Server → C | ID=1024, Type=msg, DisplayName=Server, Content=name111 joined discord.verified-1.
    
    >No.     Time           Source                Destination           Protocol Length Info
    1667 46.939546      147.229.208.84        147.229.8.244         IPK24-CHAT 45     C → Server | Type=confirm, RefID=1024
    
    >No.     Time           Source                Destination           Protocol Length Info
    1668 46.940403      147.229.8.244         147.229.208.84        IPK24-CHAT 96     Server → C | ID=1280, Type=reply, Result=OK, RefID=3, Content=Channel discord.verified-1 successfully joined.
    
    >No.     Time           Source                Destination           Protocol Length Info
    1669 46.941215      147.229.208.84        147.229.8.244         IPK24-CHAT 45     C → Server | Type=confirm, RefID=1280
    
    >No.     Time           Source                Destination           Protocol Length Info
    2008 54.794089      147.229.8.244         147.229.208.84        IPK24-CHAT 84     Server → C | ID=1536, Type=msg, DisplayName=Server, Content=anop joined discord.verified-1.
    
    >No.     Time           Source                Destination           Protocol Length Info
    2009 54.795131      147.229.208.84        147.229.8.244         IPK24-CHAT 45     C → Server | Type=confirm, RefID=1536
    
    >No.     Time           Source                Destination           Protocol Length Info
    2094 56.623736      147.229.208.84        147.229.8.244         IPK24-CHAT 57     C → Server | ID=4, Type=msg, DisplayName=name111, Content=bye
    
    >No.     Time           Source                Destination           Protocol Length Info
    2095 56.624567      147.229.8.244         147.229.208.84        IPK24-CHAT 60     Server → C | Type=confirm, RefID=4
    
    >No.     Time           Source                Destination           Protocol Length Info
    2450 62.596215      147.229.208.84        147.229.8.244         IPK24-CHAT 45     C → Server | ID=5, Type=bye
    
    >No.     Time           Source                Destination           Protocol Length Info
    2451 62.597100      147.229.8.244         147.229.208.84           IPK24-CHAT 60     Server → C | Type=confirm, RefID=5
    

### Bibliography
- Theory and general introduction to tcp/udp
    - https://habr.com/ru/articles/711578/
    - https://habr.com/ru/articles/732794/
    - https://habr.com/ru/companies/ruvds/articles/759988/
- Examples of Client Server Application Implementation 
    - https://www.youtube.com/watch?v=WfD5FMJSoMg&t=539s
    - https://www.youtube.com/watch?v=QohqDyTjclw
- Documentation of the most important libraries in use
    - https://learn.microsoft.com/en-us/dotnet/api/System.Net.Sockets.UdpClient?view=net-8.0
    - https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient?view=net-8.0