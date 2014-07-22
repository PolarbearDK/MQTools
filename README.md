MQTools
=======
Command line tool for copying, moving, deleting, altering &amp; viewing MSMQ messages. 

````
Use MQTools -? for usage.
Use MQTools -?? <command> for help for a specific command.
````

Sample command lines:

````
MQTools -source MySourceQueue copy -to MyDestinationMQQueue
MQTools -source MySourceQueue copy -to MyDestinationMQQueue where body -contains PriceEvent 
MQTools -source MySourceQueue delete where extension -contains NotImplementedException 
MQTools -sourcequeue Error copy -to Error2 where body -contains PriceEvent and header -matches .*NullReferenceException.*
````

Complex real life example. Note! this is *one* command line from a powershell script. 
The back-tick (`) symbol is the PowerShell line-continuation character.

````
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
````
