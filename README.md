MQTools
=======
Command line tool for copying, moving, deleting, altering &amp; viewing MSMQ messages. 

```
Use MQTools -? for usage.
Use MQTools -?? <command> for help for a specific command.
```

##Sample command lines:

```
MQTools -source MySourceQueue copy -to MyDestinationMQQueue
MQTools -source MySourceQueue copy -to MyDestinationMQQueue where body -contains PriceEvent 
MQTools -source MySourceQueue delete where extension -contains NotImplementedException 
MQTools -sourcequeue Error copy -to Error2 where body -contains PriceEvent and header -matches .*NullReferenceException.*
```

##Commands
A command specifies what you want to do with each message. The basic commands are:
* **Copy** Copies the message to another queue.
* **Delete** Delete the message from the source queue.
* **Move** Moves the message to another queue (this is actually a copy followed by a delete) 
* **Print** Output part of the message as text.
* **Count** Count messages.
* **Alter** Change part of message. 

##Criteria
Each command has a criteria, and only if criteria evaluates to true then the command is performed. If no criteria is specified then the criteria is always performed. Evaluation is done against a specific part of the MSMQ message:
- Body (default)
- Extension 
- Label
- Id
- CorrelationId
- Header: Header is "Extension" parsed as a NServiceBus header key/value xml. Use -HeaderKey <key> to specify the "key" part. Evaluation is done against the "value" part.

##Basic command usage 
Copy all messages from one queue to another queue:
```
MQTools -source MySourceQueue copy -to MyDestinationMQQueue
```

Copy messages that matches a criteria from one queue to another queue 
```
MQTools -source MySourceQueue copy -to MyDestinationMQQueue where body -contains PriceChangedEvent 
```



###Multiple commands
You are allowed to pass multiple commands to MQTools, but please note that each command has it's own criteria. Each message taken from the source queue is passed to each command in the order specified on the command line. If a command removes message from source queue (Move or Delete command) then message is considered "handled" and not passed to remaining commands.

##Queue access strategy
MQTools provide 2 different approaches to accessing the source queue.

###Cursor (default)
A read only cursor is used to read messages from queue.
Prefereble when source queue is not changed, or only doing a few changes.
- Fast.
- Able to access remote queues.
- Moves and Deletes are slow (Delete by ID).
- Throws exception if other processes changes queue while cursor is open.

###Receive (and send to end) 
Messages are Received and Sent back to end of queue.
Prefereble when source queue is large, or doing a lot of moves and/or deletes.
- 2-3 times slower than Cursor.
- MSMQ ID of message changes.
- Only local queue supported.
- Moves and Deletes are no-op's. 
- No concurrency issues.
  
##Complex real life example. 
**Note!** this is *one* command line from a powershell script. 
The back-tick (`) symbol is the PowerShell line-continuation character.

```
MQTools.exe -SourceQueue Error `
	move -To error.permanent   where -OlderThan 1.00:00:00 `
	move -To erroritemassembly where Body -Contains CreateItemAssembly `
	move -To error.receipt     where Body -Contains MissingItemLocalTransferPriceInfoCommand `
	move -To error.receipt     where Body -Contains CreateItemLocalWithInventoryAdjustmentCommand `
	move -To error.item        where Body -Contains AllLinesAddedToCentralReportEvent `
	move -To error.item        where Body -Contains CreateCentralReportLineCommand `
	move -To error.salesplan   where Body -Contains CreateSalesPlanCommand `
	move -To error.item        where Body -Contains CreateItemCodeShiftItemLocalCommand `
	move -To error.permanent   where Extension -Contains NotImplementedException
```
