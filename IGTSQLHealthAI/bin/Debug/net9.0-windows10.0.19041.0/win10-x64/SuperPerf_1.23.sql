------------------------------------------------------------------------------------------------------------------------
-- SuperPerf.sql
-- 
-- * Consolidated SQL Performance Scripts *
-- you now run once to gather complete instance data!
-- (for SQL 2014 and up)
--
-- Including:
-- 
-- 1. SQL Instance/Database Configuration Checks v0.2
-- 2. Top SPs By Average Execution Times
-- 3. SQL Index Fragmentation and Last Statistics Updates
-- 4. SQL Index Usage
-- 5. SQL Missing Indexes
-- 6. SQL Average IO Stalls
-- 7. SQL Waits
--
-- v1.2 - 7/13/2023 D. Rucinski
--        FIXES in v1.1
--        a. datatype on Missing Indexes UserCost column is now FLOAT (was incorrectly set to DATETIME)
--        b. Optimize for Ad Hoc Workloads now reports "Enabled" when set to True (was reporting "Disabled" due to using wrong configuration ID)
--        c. SQL Update Level now correctly utilizes the "ProductLevel" property if "ProductUpdateLevel" is NULL 
--            - this was incorrectly reporting in two scenarios:
--                1. prior to SQL 2014 SP1 CU3, the "ProductUpdateLevel" property did not exist
--                2. SQL 2014+ instances with SP updates but not CU updates have NULL for ProductUpdateLevel
--        d. added a [data_id] identity column to @InstanceData to force results to consistently appear in desired order
--        e. corrected "RNG" database to be "RNGDatabase" in IGT Databases list
--        f. added RG_AML database (Responsible Gaming/Anti-Money Laundering) into IGT Databases list
-- 
--        FIXES in v1.2
--        a. Missing Indexes now sorts by database first, then index impact
--        b. Added CLM, iReserve, and PmDataArchive to the database list coverage
--
-- v1.21b - 9/20/2023 D. Rucinski
--        FIXES in v1.21b
--        a. removed IGT DB_Maintenance from standard review (this can be reenabled by un-commenting the INSERT line for IGT_DBMaintenance
--           in the IGT Databases list
--        b. corrected spelling of "Drop Utility" database to "DropUtility" in the IGT Databases list
--        c. added SBDB and S2STransport databases into IGT Databases list
--
-- v1.22a - 10/15/2023 D. Rucinski
--        Fine-tuned data column order and datatypes for better readability  
--
-- v1.23 - 3/25/2024 D. Rucinski
--        a. Modified for Quip use
--        b. TOP SPs modified to allow SP Names up to 100 chars
--        c. Index Fragmentation/Last Stats updates will only show indexes with Page Count > 1000    
--        d. Missing Indexes will only show indexes with Index Impact > 10000    
------------------------------------------------------------------------------------------------------------------------
SET NOCOUNT ON

--------------------------------------------------------------------
-- create temp tables and variables
--------------------------------------------------------------------
DECLARE 
@SQLVersionString VARCHAR(300),
@SQLVersion VARCHAR(100),
@SQLBuild VARCHAR(50),
@StartPos SMALLINT,
@EndPos SMALLINT,
@ActualValue VARCHAR(100),
@SQLUpdateLevel VARCHAR(50) = 'RTM',
@InstanceName VARCHAR(50),
@DBname VARCHAR(50),
@DBID INT,
@TableName VARCHAR(50),
@FunctionalityStatus VARCHAR(10),
@Separator VARCHAR(30) = ' ',
@EnabledFlags VARCHAR (100),
@FileCount SMALLINT,
@FileSizes VARCHAR (100),
@breaker SMALLINT,
@TimeRun DATETIME,
@sqlQuery VARCHAR(4000)

--pull timestamp for script runs
SET @TimeRun = GETDATE()

-- create list of IGT Databases for which to gather data - ignoring SQL system DBs and non-IGT user databases
DECLARE @IGTDatabases AS TABLE (
    [db_name] VARCHAR(30),
    [product_name] VARCHAR(50),
    [step_complete] SMALLINT
)
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('Accounting','Machine Accounting')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('ADI_CTA','ADI for Cage and Table Accounting')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('ADI_MA','ADI for Machine Accounting')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('ADI_PM','ADI for Patron Management')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('Casino','Intelligent Offer')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('Common','Common')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('CLM','Credit Line Monitoring')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('CTA','Cage and Table Accounting')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('DropUtility','Drop Utility')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('Ezpay','Ezpay')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('EzpayMetaRS','Ezpay Report Services')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('GiftPoints','Gift Points')
--INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('IGT_DBMaintenance','DBMaintenance')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('IO','Intelligent Offer')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('iReserve','iReserve')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('MobileDashboard','Mobile Dashboard')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('MobileFramework','Mobile Dashboard')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('PlayerTracking','Player Tracking')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('PlayerManagement','Patron Management')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('PMDataArchive','Patron Management Archive')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('Quartz','Intelligent Offer')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('RG_AML','Responsible Gaming - Anti-Money Laundering')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('RNGDatabase','Random Number Generator')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('S2STransport','S2S Transport (ABS)')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('SBDB','SB Database')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('TableManager','TableManager')
INSERT INTO @IGTDatabases ([db_name],[product_name]) VALUES ('UMS','User Management Service')

-- create and populate table to hold DB Names, DB IDs, and step completion tracking
DECLARE @DBList TABLE
(
    [db_name] VARCHAR(50),
    [database_id] SMALLINT,
    [step_complete] SMALLINT
)

INSERT INTO @DBList
SELECT [db_name], DB_ID([db_name]), 0
FROM @IGTDatabases
WHERE DB_ID([db_name]) IS NOT NULL
ORDER BY [db_name] ASC

-- create tables for Step 1 datasets (SQL Configuration Checks)
DECLARE @InstanceData AS TABLE (
    [data_id] INT IDENTITY(1,1),
    [scope] VARCHAR(50),
    [config_item] VARCHAR(50),
    [recommended_value] VARCHAR(100),
    [actual_value] VARCHAR(100),
    [time_run] SMALLDATETIME
)

-- create table for Step 2 datasets (Top SPs by Elapsed Time)
DECLARE @TopSPs AS TABLE (
    [db_name] VARCHAR(50), 
    [sp_name] VARCHAR(100),
    [avg_physical_reads] BIGINT,
    [avg_logical_reads] BIGINT,
    [avg_cpu_time] BIGINT,
    [execution_count] BIGINT,
    [total_elapsed_time] BIGINT,
    [avg_elapsed_time_ms] BIGINT,
    [last_elapsed_time_ms] BIGINT,
    [when_plan_cached] DATETIME,
    [time_run] SMALLDATETIME
)

-- create table for Step 3 datasets (Index Fragmentation and Last Stats Updates)
DECLARE @StatsFrag AS TABLE (
    [db_name] VARCHAR(50), 
    [schema_name] VARCHAR(50),
    [table_name] VARCHAR(100),
    [index_name] VARCHAR(200),
    [page_count] BIGINT,
    [percent_avg_fragmentation] DECIMAL (16,2),
    [row_mods] BIGINT,
    [row_count] BIGINT,
    [percent_row_mods] DECIMAL (16,0),
    [last_statistics_update] DATETIME,
    [time_run] SMALLDATETIME
)

-- create table for Step 4 datasets (Index Usage)
DECLARE @IndexUsage AS TABLE (
    [db_name] VARCHAR(50), 
    [table_name] VARCHAR(200),
    [index_name] VARCHAR(200),
    [page_count] BIGINT,
    [user_seeks] BIGINT,
    [user_scans] BIGINT,
    [scan_pct] DECIMAL (16,0),
    [user_lookups] BIGINT,
    [user_updates] BIGINT,
    [last_user_seek] DATETIME,
    [last_user_scan] DATETIME,
    [last_user_lookup] DATETIME,
    [last_user_update] DATETIME,
    [time_run] SMALLDATETIME
)

-- create table for Step 5 dataset (Missing Indexes)
DECLARE @MissingIndexes AS TABLE (
    [db_name] VARCHAR(50),
    [index_impact] FLOAT,
    [full_object_name] NVARCHAR(4000),
    [table_name] NVARCHAR(4000),
    [equality_columns] NVARCHAR(4000),
    [inequality_columns] NVARCHAR(4000),
    [included_columns] NVARCHAR(4000),
    [compiles] BIGINT,
    [seeks] BIGINT,
    [last_user_seek] DATETIME,
    [user_cost] FLOAT,
    [user_impact] FLOAT,

    [time_run] SMALLDATETIME
)

-- create table for Step 6 dataset (Average IO Stalls)
DECLARE @AvgIObyFile AS TABLE (
    [db_name] VARCHAR(50), 
    [db_file_name] VARCHAR(50),
    [io_stall_read_ms] BIGINT,
    [num_of_reads] BIGINT,
    [avg_read_stall_ms] BIGINT,
    [io_stall_write_ms] BIGINT,
    [num_of_writes] BIGINT,
    [avg_write_stall_ms] BIGINT,
    [io_stalls] BIGINT,
    [total_io] BIGINT,
    [avg_io_stall_ms] BIGINT,
    [time_run] SMALLDATETIME
)

-- create table for Step 7 dataset (SQL Waits)
DECLARE @SQLWaits AS TABLE (
    [wait_type] NVARCHAR(50), 
    [wait_s] DECIMAL (16,2),
    [resource_s] DECIMAL (16,2),
    [signal_s] DECIMAL (16,2),
    [wait_count] BIGINT,
    [percentage] DECIMAL (5,2),
    [avg_wait_s] DECIMAL (16,4),
    [avg_res_s] DECIMAL (16,4),
    [avg_sig_s] DECIMAL (16,4),
    [time_run] SMALLDATETIME
)

-- create table for DBCC Tracestatus
DECLARE @TraceFlags AS TABLE (
    TraceFlag INT,
    [Status] BIT,
    [Global] BIT,
    [Session] BIT
)

-- create and populate table for DB Sizes
CREATE TABLE #DatabaseSizes (
    [db_name] VARCHAR(50),
    [size_in_mb] BIGINT
)

INSERT INTO #DatabaseSizes 
    select  
        DATABASE_NAME   = DB_NAME(mf.database_id),
        db_size_in_mb   = SUM(CONVERT(BIGINT,mf.size*8/1024))
    FROM  
        sys.master_files mf
    WHERE  
        mf.state = 0            -- ONLINE  
        AND HAS_DBACCESS(DB_NAME(mf.database_id)) = 1            -- Only look at databases to which we have access  
    GROUP BY mf.database_id  
    ORDER BY 1  

-- create table to pull datafile information
CREATE TABLE #DatafileInfo (
    [db_name] VARCHAR(50),
    [info_type] VARCHAR(20),
    [info_value] VARCHAR(200)
)

--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
-- 1. Instance and Database-level configuration check
--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
BEGIN
    --------------------------------------------------------------------
    -- INSTANCE LEVEL CONFIGURATION
    --------------------------------------------------------------------
    SET NOCOUNT ON
    PRINT 'STEP 1...'
    PRINT 'Instance Configuration - INSTANCE'

    --------------------------------------------------------------------
    -- create dataset for SQL instance
    --------------------------------------------------------------------    
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Instance Name','-- informational --')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Last SQL Restart','-- informational --')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Product','as specified in IGT product documentation')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Edition','SQL Standard or Enterprise')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('SQL Update Level','should be most recent SP or CU level - not RTM')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Build','see SQL Update Level')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Operating System','as specified in IGT product documentation')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('CPU Cores','minimum 8 for Patron, minimum 4 for other DBs')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Boost SQL Priority','Disabled')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Total Server Memory','minimum 32 GB for Patron, 16 GB for other DBs')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('SQL Minimum Server Memory','0% of server memory')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('SQL Maximum Server Memory','75-80% of server memory')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Minimum Query Memory','1024 or 2048 MB')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Memory Utilization','less than 95%')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Clustered','IGT does not support SQL clusters')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Always On (HADR) Enabled','IGT does not support Always On')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Optimize for Ad Hoc Workloads','ENABLED at sites with customizations')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Cost threshold for parallelism','50 for large databases, 35 for small ones')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Maxdop','0 - this lets SQL decide when to use parallelism')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Trace Flags Enabled','1117, 1118, 4199 enabled prior to SQL 2016')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('TempDB Data File Count','One data file per Core, up to 8')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('TempDB Data File Sizes (MB)','Evenly sized files')
    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('TempDB Autogrow All Files','True')
    INSERT INTO @InstanceData VALUES (@Separator,@Separator,@Separator,@Separator,@Separator)

    --------------------------------------------------------------------
    -- get SQL Product Full Version Information String
    --------------------------------------------------------------------
    SELECT @SQLVersionString = CAST(@@VERSION AS VARCHAR(300))

    --------------------------------------------------------------------
    -- get SQL Instance Name
    --------------------------------------------------------------------
    SET @InstanceName = CAST(SERVERPROPERTY('ServerName') AS VARCHAR(50))

    UPDATE @InstanceData SET scope = 'INSTANCE'
    WHERE scope IS NULL

    UPDATE @InstanceData SET actual_value = @InstanceName
    WHERE config_item = 'Instance Name'

    --------------------------------------------------------------------
    -- get SQL Last Restart time
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT sqlserver_start_time FROM master.sys.dm_os_sys_info)
    WHERE config_item = 'Last SQL Restart'

    --------------------------------------------------------------------
    -- get SQL Version
    --------------------------------------------------------------------
    SET @SQLVersion = SUBSTRING(@SQLVersionString,1,CHARINDEX('(',@SQLVersionString)-2)
    UPDATE @InstanceData SET actual_value = @SQLVersion
    WHERE config_item = 'Product'

    --------------------------------------------------------------------
    -- get SQL Edition
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = CAST(SERVERPROPERTY('Edition') AS VARCHAR(100))
    WHERE config_item = 'Edition'

    --------------------------------------------------------------------
    -- get SQL Update Level
    --------------------------------------------------------------------
    IF (SERVERPROPERTY ('ProductUpdateLevel') IS NOT NULL)
        SET @SQLUpdateLevel = CAST(SERVERPROPERTY ('ProductUpdateLevel') AS VARCHAR(100))
    ELSE
        SET @SQLUpdateLevel = CAST(SERVERPROPERTY ('ProductLevel') AS VARCHAR(100))            -- note that SQL 2014 SP1 CU3 is the first version to utilize ProductUpdateLevel

    UPDATE @InstanceData SET actual_value = @SQLUpdateLevel
    WHERE config_item = 'SQL Update Level'

    --------------------------------------------------------------------
    -- get Build
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = CAST(SERVERPROPERTY ('ProductVersion') AS VARCHAR(100))
    WHERE config_item = 'Build'

    --------------------------------------------------------------------
    -- get Operating System
    --------------------------------------------------------------------
    -- locate Windows OS section of version string
    SELECT @StartPos = CHARINDEX('W',@SQLVersionString)
    SELECT @EndPos = CHARINDEX('<',@SQLVersionString)

    SELECT @ActualValue = (SELECT SUBSTRING(@SQLVersionString, @StartPos, @EndPos - @StartPos - 1))

    IF (@ActualValue IS NOT NULL OR LEN(@ActualValue) > 0)
        -- Windows OS
        UPDATE @InstanceData SET actual_value = @ActualValue
        WHERE config_item = 'Operating System'
    ELSE
        -- Linux OS - get info from dm_os_host_info
        UPDATE @InstanceData SET actual_value = (SELECT host_distribution + ' ' + host_release FROM master.sys.dm_os_host_info)
        WHERE config_item = 'Operating System'

    --------------------------------------------------------------------
    -- get Cores
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT cpu_count FROM master.sys.dm_os_sys_info)
    WHERE config_item = 'CPU Cores'

    --------------------------------------------------------------------
    -- get Boost SQL Priority status
    --------------------------------------------------------------------
    IF ((SELECT CAST(Value AS VARCHAR) FROM master.sys.configurations WHERE configuration_id = 1517)=1)  -- "priority boost"
        SET @FunctionalityStatus = 'ENABLED'
    ELSE
        SET @FunctionalityStatus = 'Disabled'

    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
    WHERE config_item = 'Boost SQL Priority'

    --------------------------------------------------------------------
    -- get Total Server Physical Memory
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT CAST(total_physical_memory_kb/1000/1000 AS VARCHAR) + ' GB' FROM master.sys.dm_os_sys_memory)
    WHERE config_item = 'Total Server Memory'

    --------------------------------------------------------------------
    -- get SQL Minimum Server Memory
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT CAST(Value AS VARCHAR) + ' MB' FROM master.sys.configurations WHERE configuration_id = 1543)  -- min server memory (MB)
    WHERE config_item = 'SQL Minimum Server Memory'

    -- Min Memory and Max Memory
    -- see https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/server-memory-server-configuration-options?view=sql-server-2016
    --------------------------------------------------------------------
    -- get SQL Maximum Server Memory
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT CAST(Value AS VARCHAR) + ' MB' FROM master.sys.configurations WHERE configuration_id = 1544)  -- max server memory (MB)
    WHERE config_item = 'SQL Maximum Server Memory'

    --------------------------------------------------------------------
    -- get SQL Maximum Query Memory
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT CAST(Value AS VARCHAR) + ' MB' FROM master.sys.configurations WHERE configuration_id = 1540)  -- minimum query memory (MB)
    WHERE config_item = 'Minimum Query Memory'

    --------------------------------------------------------------------
    -- get Memory Utilization
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT CAST((total_physical_memory_kb-available_physical_memory_kb) * 100/total_physical_memory_kb AS VARCHAR) + '%' FROM master.sys.dm_os_sys_memory)
    WHERE config_item = 'Memory Utilization'

    --------------------------------------------------------------------
    -- get Clustering status
    --------------------------------------------------------------------
    IF (CAST(SERVERPROPERTY('IsClustered') AS SMALLINT) = 1)
        SET @FunctionalityStatus = 'YES'
    ELSE
        SET @FunctionalityStatus = 'NO'

    UPDATE @InstanceData SET actual_value = @FunctionalityStatus 
    WHERE config_item = 'Clustered'

    --------------------------------------------------------------------
    -- get HADR (Always On) status
    --------------------------------------------------------------------
    IF (CAST(SERVERPROPERTY('IsHADREnabled') AS SMALLINT) = 1)
        SET @FunctionalityStatus = 'YES'
    ELSE
        SET @FunctionalityStatus = 'NO'

    UPDATE @InstanceData SET actual_value = @FunctionalityStatus 
    WHERE config_item = 'Always On (HADR) Enabled'

    --------------------------------------------------------------------
    -- get Optimize for Ad Hoc Workloads Status
    --------------------------------------------------------------------
    IF ((SELECT Value FROM master.sys.configurations WHERE configuration_id = 1581) = 1)  -- Optimize for Ad Hoc Workloads; note that Enable Ad Hoc Distributed Queries is 16391
        SET @FunctionalityStatus = 'Enabled'
    ELSE
        SET @FunctionalityStatus = 'Disabled'
    
    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
    WHERE config_item = 'Optimize for Ad Hoc Workloads'

    --------------------------------------------------------------------
    -- get Cost Threshold for Parallelism
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT CAST(Value AS VARCHAR) FROM master.sys.configurations WHERE configuration_id = 1538)  -- cost threshold for parallelism
    WHERE config_item = 'Cost threshold for parallelism'

    --------------------------------------------------------------------
    -- get Maxdop (Max Degree of Parallelism)
    --------------------------------------------------------------------
    UPDATE @InstanceData SET actual_value = (SELECT CAST(Value AS VARCHAR) FROM master.sys.configurations WHERE configuration_id = 1539)  -- max degree of parallelism
    WHERE config_item = 'Maxdop'

    --------------------------------------------------------------------
    -- get Enabled Trace Flags
    --------------------------------------------------------------------
    INSERT @TraceFlags EXEC ('DBCC TRACESTATUS WITH NO_INFOMSGS')    -- NOTE that trace flag behavior changes beginning with SQL 2016 (read up on it!)

    SELECT @EnabledFlags = (COALESCE(@EnabledFlags + ', ', '') + CAST(TraceFlag AS VARCHAR)) FROM @TraceFlags WHERE [Status] = 1 AND [Global] = 1

    IF (@EnabledFlags IS NULL OR LEN(@EnabledFlags) = 0)
        SET @EnabledFlags = '<none enabled>'

    UPDATE @InstanceData SET actual_value = @EnabledFlags
    WHERE config_item = 'Trace Flags Enabled'

    --------------------------------------------------------------------
    -- get TempDB Data File Count
    --------------------------------------------------------------------
    SELECT @ActualValue = COUNT(*) FROM tempdb.sys.sysfiles WHERE groupid = 1

    UPDATE @InstanceData SET actual_value = @ActualValue
    WHERE config_item = 'TempDB Data File Count' AND scope = 'INSTANCE'

    --------------------------------------------------------------------
    -- get TempDB Data File Sizes
    --------------------------------------------------------------------
    SELECT @FileSizes = (COALESCE(@FileSizes + ', ', '') + CAST(size AS VARCHAR)) FROM tempdb.sys.sysfiles WHERE groupid = 1

    UPDATE @InstanceData SET actual_value = @FileSizes
    WHERE config_item = 'TempDB Data File Sizes (MB)' AND scope = 'INSTANCE'

    --------------------------------------------------------------------
    -- get TempDB Autogrow All Files status
    --------------------------------------------------------------------
    IF (CAST(SUBSTRING(CAST(SERVERPROPERTY('ProductVersion') AS VARCHAR), 1,2) AS INT) >= 13)        -- only check for SQL 2016 or later
        BEGIN
            IF ((SELECT is_autogrow_all_files FROM tempdb.sys.filegroups WHERE [name] = 'PRIMARY') = 1)
                SET @FunctionalityStatus = 'True'
            ELSE
                SET @FunctionalityStatus = 'False'
        END

    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
    WHERE config_item = 'TempDB Autogrow All Files' AND scope = 'INSTANCE'

    --------------------------------------------------------------------
    -- DATABASE LEVEL CONFIGURATION
    --------------------------------------------------------------------
    SET NOCOUNT ON
    SET @breaker = 1

    --------------------------------------------------------------------
    -- Loop through IGT Databases
    --------------------------------------------------------------------
    WHILE EXISTS (SELECT TOP 1 [db_name] FROM @DBList WHERE step_complete = 0)
        BEGIN
            SET NOCOUNT ON
            --------------------------------------------------------------------
            -- Get Database Name for this data pull
            --------------------------------------------------------------------    
            SELECT TOP 1 @DBname = [db_name] FROM @DBList WHERE step_complete = 0

            IF EXISTS (SELECT Name FROM master.sys.databases WHERE [name] = @DBname)
                BEGIN
                    PRINT 'Database Configuration - ' + @DBname
                    --------------------------------------------------------------------
                    -- create dataset for current database
                    --------------------------------------------------------------------    
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Database Size','-- informational --')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Compatibility Level','highest available Compatibility level for SQL')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Recovery Model','typically FULL for at least Ezpay, Table Manager')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Data Filegroup Count','Accounting has fgMeter and fgTrans Filegroups, Ezpay has FG1')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Data Files in Primary FG','One data file per Core, up to 8 (typically Patron only)')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Data File Sizes (MB)','Evenly sized files')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Autogrow All Files','True - when there are multiple data files in a filegroup')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Auto Close','False')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Auto Shrink','False')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Auto Create Statistics','True')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Auto Update Statistics','True')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Auto Update Statistics Asynchronously','True')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Page Verify','CHECKSUM')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Trustworthy','True for Advantage DBs')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Broker Enabled','True for Advantage DBs')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Database Read-Only','False')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Database State','-- informational --')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Encryption Enabled','-- informational --')
                    INSERT INTO @InstanceData (config_item,recommended_value) VALUES ('Access Mode','MULTI_USER')
                    INSERT INTO @InstanceData VALUES (@Separator,@Separator,@Separator,@Separator,@Separator)

                    --------------------------------------------------------------------
                    -- Update Database Name for new data pull
                    --------------------------------------------------------------------
                    UPDATE @InstanceData SET scope = @DBname
                    WHERE scope IS NULL

                    --------------------------------------------------------------------
                    -- get Database Size
                    --------------------------------------------------------------------
                    SELECT @ActualValue = CASE
                    --    db_size = CASE  
                                        -- 1024 MB is 1 GB, 1024 GB is 1 TB (1024 * 1024 is 1048576)
                                        WHEN size_in_mb >= 1048576 THEN CAST(CONVERT(DECIMAL(16,1),(size_in_mb / 1024 / 1024)) AS VARCHAR) + ' TB'
                                        WHEN size_in_mb >= 1024 AND size_in_mb < 1048576 THEN CAST(CONVERT(DECIMAL(16,1),(size_in_mb / 1024)) AS VARCHAR) + ' GB'
                                        ELSE CAST(CONVERT(DECIMAL(16,0),size_in_mb) AS VARCHAR) + ' MB'
                                    END
                    FROM #DatabaseSizes WHERE [db_name] = @DBname

                    UPDATE @InstanceData SET actual_value = @ActualValue
                    WHERE config_item = 'Database Size' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Compatibility Level
                    --------------------------------------------------------------------
                    SELECT @ActualValue =    CASE
                                                WHEN [compatibility_level] = '90' THEN '90  (SQL 2005)'
                                                WHEN [compatibility_level] = '100' THEN '100  (SQL 2008)'
                                                WHEN [compatibility_level] = '105' THEN '105  (SQL 2008 R2)'
                                                WHEN [compatibility_level] = '110' THEN '110  (SQL 2012)'
                                                WHEN [compatibility_level] = '120' THEN '120  (SQL 2014)'
                                                WHEN [compatibility_level] = '130' THEN '130  (SQL 2016)'
                                                WHEN [compatibility_level] = '140' THEN '140  (SQL 2017)'
                                                WHEN [compatibility_level] = '150' THEN '150  (SQL 2019)'
                                                WHEN [compatibility_level] = '160' THEN '160  (SQL 2022)'
                                                ELSE '<unable to determine>'
                                            END
                    FROM sys.databases WHERE [name] = @DBname

                    UPDATE @InstanceData SET actual_value = @ActualValue
                    WHERE config_item = 'Compatibility Level' AND scope = @DBname
                
                    --------------------------------------------------------------------
                    -- get Recovery Model
                    --------------------------------------------------------------------
                    UPDATE @InstanceData SET actual_value = (SELECT recovery_model_desc FROM sys.databases WHERE [name] = @DBname)
                    WHERE config_item = 'Recovery Model' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Data Filegroup Count
                    --------------------------------------------------------------------
                    SET @sqlQuery  = 'INSERT INTO #DatafileInfo ' + CHAR(10)
                    SET @sqlQuery += '  SELECT ''' + @DBname + ''', ''FilegroupCount'', COUNT(*) FROM [' + @DBname + '].sys.sysfiles WHERE groupid > 0'
                    EXEC (@sqlQuery)

                    UPDATE @InstanceData SET actual_value = (SELECT info_value FROM #DatafileInfo WHERE [db_name] = @DBname AND info_type = 'FilegroupCount')
                    WHERE config_item = 'Data Filegroup Count' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Data File Count
                    --------------------------------------------------------------------
                    SET @sqlQuery  = 'INSERT INTO #DatafileInfo ' + CHAR(10)
                    SET @sqlQuery += '  SELECT ''' + @DBname + ''', ''FileCount'', COUNT(*) FROM [' + @DBname + '].sys.sysfiles WHERE groupid = 1'
                    EXEC (@sqlQuery)

                    UPDATE @InstanceData SET actual_value = (SELECT info_value FROM #DatafileInfo WHERE [db_name] = @DBname AND info_type = 'FileCount')
                    WHERE config_item = 'Data Files in Primary FG' AND scope = @DBname
                               
                    --------------------------------------------------------------------
                    -- get Data File Sizes
                    --------------------------------------------------------------------
                    SET @sqlQuery  = 'INSERT INTO #DatafileInfo ' + CHAR(10)
                    SET @sqlQuery += '  SELECT ''' + @DBname + ''', ''FileSizes'', size FROM [' + @DBname + '].sys.sysfiles WHERE groupid = 1'
                    EXEC (@sqlQuery)

                    SET @FileSizes = ''
                    SELECT @FileSizes = COALESCE(@FileSizes + ', ','') + info_value FROM #DatafileInfo WHERE [db_name] = @DBname AND info_type = 'FileSizes'
                    SET @FileSizes = SUBSTRING(@FileSizes,3,LEN(@FileSizes))

                    UPDATE @InstanceData SET actual_value = @FileSizes 
                    WHERE config_item = 'Data File Sizes (MB)' AND scope = @DBname 

                    --------------------------------------------------------------------
                    -- get Autogrow All Files status
                    --------------------------------------------------------------------
                    IF (CAST(SUBSTRING(CAST(SERVERPROPERTY('ProductVersion') AS VARCHAR), 1,2) AS INT) >= 13)        -- only check for SQL 2016 or later
                        BEGIN
                            SET @sqlQuery  = 'INSERT INTO #DatafileInfo ' + CHAR(10)
                            SET @sqlQuery += '  SELECT ''' + @DBname + ''', ''AutogrowAllFiles'', is_autogrow_all_files FROM [' + @DBname + '].sys.filegroups WHERE name = ''PRIMARY'''
                            EXEC (@sqlQuery)
        
                            IF ((SELECT info_value FROM #DatafileInfo WHERE [db_name] = @DBname AND info_type = 'AutogrowAllFiles') = 1)
                                SET @FunctionalityStatus = 'True'
                            ELSE
                                SET @FunctionalityStatus = 'False'
                        END

                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Autogrow All Files' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Auto Close Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_auto_close_on FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Auto Close' AND scope = @DBname
                
                    --------------------------------------------------------------------
                    -- get Auto Create Stats Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_auto_create_stats_on FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Auto Create Statistics' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Auto Shrink Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_auto_shrink_on FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Auto Shrink' AND scope = @DBname
                
                    --------------------------------------------------------------------
                    -- get Auto Update Stats Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_auto_update_stats_on FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Auto Update Statistics' AND scope = @DBname
                
                    --------------------------------------------------------------------
                    -- get Auto Update Stats Asynchronously Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_auto_update_stats_async_on FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Auto Update Statistics Asynchronously' AND scope = @DBname
                
                    --------------------------------------------------------------------
                    -- get Page Verify option
                    --------------------------------------------------------------------
                    UPDATE @InstanceData SET actual_value = (SELECT page_verify_option_desc FROM master.sys.databases WHERE [name] = @DBname)
                    WHERE config_item = 'Page Verify' AND scope = @DBname
                
                    --------------------------------------------------------------------
                    -- get Trustworthy Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_trustworthy_on FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Trustworthy' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Service Broker Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_broker_enabled FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Broker Enabled' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Database Read-Only Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_read_only FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Database Read-Only' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Database State
                    --------------------------------------------------------------------
                    UPDATE @InstanceData SET actual_value = (SELECT state_desc FROM master.sys.databases WHERE [name] = @DBname)
                    WHERE config_item = 'Database State' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Encryption Status
                    --------------------------------------------------------------------
                    IF ((SELECT is_encrypted FROM master.sys.databases WHERE [name] = @DBname) = 1)
                        SET @FunctionalityStatus = 'True'
                    ELSE
                        SET @FunctionalityStatus = 'False'
    
                    UPDATE @InstanceData SET actual_value = @FunctionalityStatus
                    WHERE config_item = 'Encryption Enabled' AND scope = @DBname

                    --------------------------------------------------------------------
                    -- get Access Mode
                    --------------------------------------------------------------------
                    UPDATE @InstanceData SET actual_value = (SELECT user_access_desc FROM master.sys.databases WHERE [name] = @DBname)
                    WHERE config_item = 'Access Mode' AND scope = @DBname
                END

            --------------------------------------------------------------------
            -- set step complete for this db, enabling move on to next DB item
            --------------------------------------------------------------------
            UPDATE @DBList SET step_complete = 1 WHERE [db_name] = @DBname

            --------------------------------------------------------------------
            -- check the loop count and throw circuitbreaker if caught in loop
            --------------------------------------------------------------------
            SET @breaker = @breaker + 1
            IF @breaker = 30 BREAK
        END
    
    --------------------------------------------------------------------
    -- clean up temp tables
    --------------------------------------------------------------------
    DROP TABLE #DatabaseSizes 
    DROP TABLE #DatafileInfo

    --------------------------------------------------------------------
    -- update runtime and return collected data
    --------------------------------------------------------------------
    SET NOCOUNT ON
    UPDATE @InstanceData SET time_run = @TimeRun
    SELECT 
        LEFT(RTRIM([scope]),50) AS [scope], 
        LEFT(RTRIM([config_item]),50) AS [config_item],
        LEFT(RTRIM([recommended_value]),100) AS [recommended_value],
        LEFT(RTRIM([actual_value]),100) AS [actual_value],
        [time_run]
    FROM @InstanceData
    ORDER BY [data_id]

    PRINT CHAR(10)
END

--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
-- 2. Top Stored Procedures by Average Execution Times
--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
BEGIN
    SET NOCOUNT ON
    PRINT 'STEP 2...'

    SET @breaker = 1
    --------------------------------------------------------------------
    -- Loop through IGT Databases
    --------------------------------------------------------------------
    WHILE EXISTS (SELECT TOP 1 [db_name] FROM @DBList WHERE step_complete = 1)
        BEGIN
            SET NOCOUNT ON
            --------------------------------------------------------------------
            -- Get Database Name for this data pull
            --------------------------------------------------------------------    
            SELECT TOP 1 @DBname = [db_name], @DBID = database_id FROM @DBList WHERE step_complete = 1
            PRINT 'Top 25 - ' + @DBname
             
            --------------------------------------------------------------------
            -- set up query with dynamic SQL and current DB info
            --------------------------------------------------------------------    
            SET @sqlQuery  = 'SELECT TOP ( 25 ) ' + CHAR(10)
            SET @sqlQuery += '''' + @DBname + ''' AS [db_name], ' + CHAR(10)
            SET @sqlQuery += 'p.name AS [sp_name] , ' + CHAR(10)
            SET @sqlQuery += 'qs.[total_physical_reads] / qs.[execution_count] AS [avg_physical_reads] ,' + CHAR(10)
            SET @sqlQuery += 'qs.[total_logical_reads] / qs.[execution_count] AS [avg_logical_reads] ,' + CHAR(10)
            SET @sqlQuery += 'qs.[total_worker_time] / qs.[execution_count] AS [avg_cpu_time] ,' + CHAR(10)
            SET @sqlQuery += 'qs.[execution_count] , ' + CHAR(10)
            SET @sqlQuery += 'qs.[total_elapsed_time] ,' + CHAR(10)
            SET @sqlQuery += 'qs.[total_elapsed_time] / qs.[execution_count] / 1000 AS [avg_elapsed_time_ms] , ' + CHAR(10)
            SET @sqlQuery += 'qs.[last_elapsed_time] / 1000 AS [last_elapsed_time_ms] , ' + CHAR(10)
            SET @sqlQuery += 'qs.[cached_time] AS [when_plan_cached], ' + CHAR(10)
            SET @sqlQuery += 'CONVERT(SMALLDATETIME,''' + CAST(@TimeRun AS VARCHAR) + ''') AS [time_run]' + CHAR(10)
            SET @sqlQuery += 'FROM [' + @DBname + '].sys.procedures AS p ' + CHAR(10)
            SET @sqlQuery += 'INNER JOIN [' + @DBname + '].sys.dm_exec_procedure_stats AS qs ON p.[object_id] = qs.[object_id] ' + CHAR(10)
            SET @sqlQuery += 'CROSS APPLY [' + @DBname + '].sys.dm_exec_query_plan (qs.[plan_handle]) AS qp ' + CHAR(10)
            SET @sqlQuery += 'WHERE qs.[database_id] = ' + CONVERT(VARCHAR,@DBID) + ' ' + CHAR(10)
            SET @sqlQuery += 'ORDER BY [avg_elapsed_time_ms] DESC ;'

            --------------------------------------------------------------------
            -- execute query and populate table
            --------------------------------------------------------------------    
            INSERT INTO @TopSPs EXEC (@sqlQuery)
            
            --------------------------------------------------------------------
            -- set step complete for this db, enabling move on to next DB item
            --------------------------------------------------------------------
            UPDATE @DBList SET step_complete = 2 WHERE [db_name] = @DBname

            --------------------------------------------------------------------
            -- check the loop count and throw circuitbreaker if caught in loop
            --------------------------------------------------------------------
            SET @breaker = @breaker + 1
            IF @breaker = 30 BREAK
        END

    --------------------------------------------------------------------
    -- return collected data
    --------------------------------------------------------------------
    SET NOCOUNT ON
    SELECT    [db_name], [sp_name], [avg_physical_reads], [avg_logical_reads], [avg_cpu_time], [execution_count], [total_elapsed_time], [avg_elapsed_time_ms], [last_elapsed_time_ms]
            , [when_plan_cached], [time_run]
    FROM @TopSPs
    ORDER BY [db_name], [avg_elapsed_time_ms] DESC

    PRINT CHAR(10)
END


--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
-- 3. Index Fragmentation and Last Statistics Update
--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
BEGIN
    SET NOCOUNT ON
    PRINT 'STEP 3...'

    SET @breaker = 1
    --------------------------------------------------------------------
    -- Loop through IGT Databases
    --------------------------------------------------------------------
    WHILE EXISTS (SELECT TOP 1 [db_name] FROM @DBList WHERE step_complete = 2)
        BEGIN
            SET NOCOUNT ON

            --------------------------------------------------------------------
            -- Get Database Name for this data pull
            --------------------------------------------------------------------    
            SELECT TOP 1 @DBname = [db_name] FROM @DBList WHERE step_complete = 2
            PRINT 'Index Usage - ' + @DBname

            --------------------------------------------------------------------
            -- build query using current DB Name
            --------------------------------------------------------------------    
            SET @sqlQuery  = 'USE ' + @DBname + ';' + CHAR(10)
            SET @sqlQuery += 'SELECT' + CHAR(10)
            SET @sqlQuery += '''' + @DBname + ''' AS [db_name],' + CHAR(10)
            SET @sqlQuery += 'sch.name AS [schema_name],' + CHAR(10)
            SET @sqlQuery += 't.name AS [table_name],' + CHAR(10)
            SET @sqlQuery += 'i.name AS [index_name],' + CHAR(10)
            SET @sqlQuery += 'ps.[page_count],' + CHAR(10)
            SET @sqlQuery += 'ps.[avg_fragmentation_in_percent],' + CHAR(10)
            SET @sqlQuery += 'r.rowmodctr [row_mods],' + CHAR(10)
            SET @sqlQuery += 'r.rowcnt AS [row_count],' + CHAR(10)
            SET @sqlQuery += 'CAST((r.rowmodctr * 100)/(0.1 + r.rowcnt) AS DECIMAL (16,0)) AS [percent_row_mods],' + CHAR(10)
            SET @sqlQuery += 'STATS_DATE(i.[object_id], st.[stats_id]) AS [last_statistics_update],' + CHAR(10)
            SET @sqlQuery += 'CONVERT(SMALLDATETIME,''' + CAST(@TimeRun AS VARCHAR) + ''') AS [time_run]' + CHAR(10)
            SET @sqlQuery += 'FROM sys.dm_db_index_physical_stats (DB_ID(), NULL, NULL, NULL, NULL) AS ps' + CHAR(10)
            SET @sqlQuery += 'INNER JOIN sys.tables t on t.object_id = ps.object_id' + CHAR(10)
            SET @sqlQuery += 'INNER JOIN sys.schemas sch on t.schema_id = sch.schema_id' + CHAR(10)
            SET @sqlQuery += 'INNER JOIN sys.indexes i ON i.object_id = ps.object_id' + CHAR(10)
            SET @sqlQuery += 'INNER JOIN sys.sysindexes r ON i.object_id = r.id AND i.index_id = r.indid' + CHAR(10) 
            SET @sqlQuery += 'INNER JOIN sys.stats AS st ON i.object_id = st.object_id AND i.index_id = st.stats_id' + CHAR(10)
            SET @sqlQuery += 'AND ps.index_id = i.index_id' + CHAR(10)
            SET @sqlQuery += 'WHERE ps.database_id = DB_ID()' + CHAR(10)
            SET @sqlQuery += 'AND I.name is not null' + CHAR(10)
            SET @sqlQuery += 'AND ps.avg_fragmentation_in_percent > 0' + CHAR(10)
            SET @sqlQuery += 'AND page_count > 100' + CHAR(10)                        -- we're primarily concerned with larger indexes with respect to performance

            --------------------------------------------------------------------
            -- execute query and populate table
            --------------------------------------------------------------------    
            PRINT @sqlQuery
            INSERT INTO @StatsFrag EXEC (@sqlQuery)

            --------------------------------------------------------------------
            -- set step complete for this db, enabling move on to next DB item
            --------------------------------------------------------------------
            UPDATE @DBList SET step_complete = 3 WHERE [db_name] = @DBname

            --------------------------------------------------------------------
            -- check the loop count and throw circuitbreaker if caught in loop
            --------------------------------------------------------------------
            SET @breaker = @breaker + 1
            IF @breaker = 30 BREAK
        END

    --------------------------------------------------------------------
    -- return collected data
    --------------------------------------------------------------------
    SET NOCOUNT ON
    SELECT    [db_name], [schema_name], [table_name], [index_name], [page_count], [percent_avg_fragmentation], [row_mods], [row_count], [percent_row_mods],
            [last_statistics_update], [time_run]
    FROM @StatsFrag
    WHERE [page_count] > 1000
    ORDER BY [db_name], [percent_avg_fragmentation] DESC, [table_name], [index_name]

    PRINT CHAR(10)
END


--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
-- 4. SQL Index Usage
--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
BEGIN
    SET NOCOUNT ON
    PRINT 'STEP 4...'

    SET @breaker = 1
    --------------------------------------------------------------------
    -- Loop through IGT Databases
    --------------------------------------------------------------------
    WHILE EXISTS (SELECT TOP 1 [db_name] FROM @DBList WHERE step_complete = 3)
        BEGIN
            SET NOCOUNT ON

            --------------------------------------------------------------------
            -- Get Database Name for this data pull
            --------------------------------------------------------------------    
            SELECT TOP 1 @DBname = [db_name] FROM @DBList WHERE step_complete = 3
            PRINT 'Index Usage - ' + @DBname

            --------------------------------------------------------------------
            -- build query using current DB Name
            --------------------------------------------------------------------    
            SET @sqlQuery  = 'USE ' + @DBname + ';' + CHAR(10)
            SET @sqlQuery += 'SELECT' + CHAR(10)
            SET @sqlQuery += ' ''' + @DBname + ''' AS [db_name]' + CHAR(10)
            SET @sqlQuery += ',OBJECT_NAME(us.[object_id]) AS [TableName]' + CHAR(10)
            SET @sqlQuery += ',i.[NAME] AS [IndexName]' + CHAR(10)
            SET @sqlQuery += ',ps.[used_page_count] * 8 AS [page_count]' + CHAR(10)
            SET @sqlQuery += ',us.[user_seeks]' + CHAR(10)
            SET @sqlQuery += ',us.[user_scans]' + CHAR(10)
            SET @sqlQuery += ',CAST((us.[user_scans]*100)/([user_seeks] + [user_scans] + 0.001) AS DECIMAL(3,0)) AS [scan_pct]' + CHAR(10)
            SET @sqlQuery += ',us.[user_lookups]' + CHAR(10)
            SET @sqlQuery += ',us.[user_updates]' + CHAR(10)
            SET @sqlQuery += ',us.[last_user_seek]' + CHAR(10)
            SET @sqlQuery += ',us.[last_user_scan]' + CHAR(10)
            SET @sqlQuery += ',us.[last_user_lookup]' + CHAR(10)
            SET @sqlQuery += ',us.[last_user_update]' + CHAR(10)
            SET @sqlQuery += ',CONVERT(SMALLDATETIME,''' + CAST(@TimeRun AS VARCHAR) + ''') AS [time_run]' + CHAR(10)
            SET @sqlQuery += 'FROM sys.indexes AS i' + CHAR(10)
            SET @sqlQuery += 'INNER JOIN sys.dm_db_index_usage_stats AS us ON i.[object_id] = us.[object_id] AND i.[index_id] = us.[index_id]' + CHAR(10)
            SET @sqlQuery += 'INNER JOIN sys.dm_db_partition_stats AS ps ON ps.[object_id] = i.[object_id] AND ps.[index_id] = i.[index_id]' + CHAR(10)
            SET @sqlQuery += 'WHERE OBJECTPROPERTY(us.[object_id],''IsUserTable'') = 1' + CHAR(10)
            SET @sqlQuery += 'ORDER BY [TableName], [IndexName]' + CHAR(10)

            --------------------------------------------------------------------
            -- execute query and populate table
            --------------------------------------------------------------------    
            INSERT INTO @IndexUsage EXEC (@sqlQuery)

            --------------------------------------------------------------------
            -- set step complete for this db, enabling move on to next DB item
            --------------------------------------------------------------------
            UPDATE @DBList SET step_complete = 4 WHERE [db_name] = @DBname

            --------------------------------------------------------------------
            -- check the loop count and throw circuitbreaker if caught in loop
            --------------------------------------------------------------------
            SET @breaker = @breaker + 1
            IF @breaker = 30 BREAK
        END

    --------------------------------------------------------------------
    -- return collected data
    --------------------------------------------------------------------
    SET NOCOUNT ON
    SELECT    [db_name], [table_name], [index_name], [page_count], [user_seeks], [user_scans], [scan_pct], [user_lookups], [user_updates]    
            ,[last_user_seek], [last_user_scan], [last_user_lookup], [last_user_update], [time_run]
    FROM @IndexUsage
    ORDER BY [db_name], [table_name], [index_name]

    PRINT CHAR(10)
END


--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
-- 5. SQL Missing Indexes
--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
BEGIN
    SET NOCOUNT ON
    PRINT 'STEP 5...'
    PRINT 'Missing Indexes - run once for the instance' + CHAR(10)

    --------------------------------------------------------------------
    -- retrieve build version and check if we can run the query
    --------------------------------------------------------------------
    SELECT @SQLBuild = actual_value FROM @InstanceData WHERE config_item = 'Build'
    IF CAST(LEFT(@SQLBuild, CHARINDEX('.',@SQLBuild)-1) AS INT) > 8
    BEGIN
        --------------------------------------------------------------------
        -- run query
        --------------------------------------------------------------------
        INSERT INTO @MissingIndexes
        SELECT
        [db_name] = DB_NAME(details.[database_id]),
        [index_impact] = CAST(user_seeks * avg_total_user_cost * (avg_user_impact * 0.5) AS BIGINT),
        [full_object_name] = details.[statement],
        [table_name] = REPLACE(REPLACE(REVERSE(LEFT(REVERSE(details.[statement]), CHARINDEX('.', REVERSE(details.[statement]))-1)),'[',''), ']',''),
        [equality_columns] = details.equality_columns,
        [inequality_columns] = details.inequality_columns,
        [included_columns] = details.included_columns,
        [compiles] = groupstats.unique_compiles,
        [seeks] = groupstats.user_seeks,
        [last_user_seek] = groupstats.last_user_seek,
        [user_cost] = CAST(groupstats.avg_total_user_cost AS DECIMAL(16,4)),
        [user_impact] = groupstats.avg_user_impact,
        [time_run] = CONVERT(SMALLDATETIME,CAST(@TimeRun AS VARCHAR))
        FROM sys.dm_db_missing_index_group_stats AS groupstats
        INNER JOIN sys.dm_db_missing_index_groups AS groups ON groupstats.group_handle = groups.index_group_handle
        INNER JOIN sys.dm_db_missing_index_details AS details ON groups.index_handle = details.index_handle
        ORDER BY [index_impact] DESC 
    END

    --------------------------------------------------------------------
    -- set step complete for all databases (run once per instance)
    --------------------------------------------------------------------
    UPDATE @DBList SET step_complete = 5

    --------------------------------------------------------------------
    -- return collected data
    --------------------------------------------------------------------
    SET NOCOUNT ON
    SELECT    [db_name], [index_impact], [full_object_name], [table_name], [equality_columns], [inequality_columns], [included_columns], [compiles], [seeks], [last_user_seek], 
            [user_cost], [user_impact], [time_run]
    FROM @MissingIndexes
    WHERE [index_impact] > 10000
    ORDER BY [db_name] ASC, [index_impact] DESC 
END


--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
-- 6. SQL Average Stalls
--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
BEGIN
    SET NOCOUNT ON
    PRINT 'STEP 6...'
    PRINT 'Average IO Stalls - run once for the instance' + CHAR(10)

    --------------------------------------------------------------------
    -- run query
    --------------------------------------------------------------------
    INSERT INTO @AvgIObyFile
    SELECT
          DB_NAME(vfs.database_id) AS [database_name]
        , mf.name AS [file_name]
        , io_stall_read_ms
        , num_of_reads
        , CAST(io_stall_read_ms/(1.0+num_of_reads) AS NUMERIC(10,1)) AS [avg_read_stall_ms]
        , io_stall_write_ms
        , num_of_writes
        , CAST(io_stall_write_ms/(1.0+num_of_writes) AS NUMERIC(10,1)) AS [avg_write_stall_ms]
        , io_stall_read_ms + io_stall_write_ms AS [io_stalls]
        , num_of_reads + num_of_writes AS [total_io]
        , CAST((io_stall_read_ms+io_stall_write_ms)/(1.0+num_of_reads + num_of_writes) AS NUMERIC(10,1)) AS [avg_io_stall_ms]
        , CONVERT(SMALLDATETIME,CAST(@TimeRun AS VARCHAR)) AS [time_run]
        FROM master.sys.dm_io_virtual_file_stats(null,null) AS vfs
    JOIN master.sys.master_files AS mf ON vfs.database_id = mf.database_id AND vfs.file_id = mf.file_id
    ORDER BY 1, 2, 3;

    --------------------------------------------------------------------
    -- set step complete for all databases (runs once per instance)
    --------------------------------------------------------------------
    UPDATE @DBList SET step_complete = 6

    --------------------------------------------------------------------
    -- return collected data
    --------------------------------------------------------------------
    SET NOCOUNT ON
    SELECT    [db_name], [db_file_name], [io_stall_read_ms], [num_of_reads], [avg_read_stall_ms], [io_stall_write_ms], [num_of_writes], [avg_write_stall_ms], 
            [io_stalls], [total_io], [avg_io_stall_ms], [time_run]
    FROM @AvgIObyFile

END


--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
-- 7. SQL Waits
--//////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\--
BEGIN
    SET NOCOUNT ON
    PRINT 'STEP 7...'
    PRINT 'SQL Waits - run once for the instance' + CHAR(10)

    --------------------------------------------------------------------
    -- run query
    --------------------------------------------------------------------
    ;WITH [Waits] AS
        (SELECT
            [wait_type],
            [wait_time_ms] / 1000.0 AS [wait_s],
            ([wait_time_ms] - [signal_wait_time_ms]) / 1000.0 AS [resource_s],
            [signal_wait_time_ms] / 1000.0 AS [signal_s],
            [waiting_tasks_count] AS [wait_count],
            100.0 * [wait_time_ms] / SUM ([wait_time_ms]) OVER() AS [percentage],
            ROW_NUMBER() OVER(ORDER BY [wait_time_ms] DESC) AS [row_num]
        FROM sys.dm_os_wait_stats
        WHERE [wait_type] NOT IN (
            N'BROKER_EVENTHANDLER',             N'BROKER_RECEIVE_WAITFOR',
            N'BROKER_TASK_STOP',                N'BROKER_TO_FLUSH',
            N'BROKER_TRANSMITTER',              N'CHECKPOINT_QUEUE',
            N'CHKPT',                           N'CLR_AUTO_EVENT',
            N'CLR_MANUAL_EVENT',                N'CLR_SEMAPHORE',
            N'DBMIRROR_DBM_EVENT',              N'DBMIRROR_EVENTS_QUEUE',
            N'DBMIRROR_WORKER_QUEUE',           N'DBMIRRORING_CMD',
            N'DIRTY_PAGE_POLL',                 N'DISPATCHER_QUEUE_SEMAPHORE',
            N'EXECSYNC',                        N'FSAGENT',
            N'FT_IFTS_SCHEDULER_IDLE_WAIT',     N'FT_IFTSHC_MUTEX',
            N'HADR_CLUSAPI_CALL',               N'HADR_FILESTREAM_IOMGR_IOCOMPLETION',
            N'HADR_LOGCAPTURE_WAIT',            N'HADR_NOTIFICATION_DEQUEUE',
            N'HADR_TIMER_TASK',                 N'HADR_WORK_QUEUE',
            N'KSOURCE_WAKEUP',                  N'LAZYWRITER_SLEEP',
            N'LOGMGR_QUEUE',                    N'ONDEMAND_TASK_QUEUE',
            N'PWAIT_ALL_COMPONENTS_INITIALIZED',
            N'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP',
            N'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
            N'REQUEST_FOR_DEADLOCK_SEARCH',     N'RESOURCE_QUEUE',
            N'SERVER_IDLE_CHECK',               N'SLEEP_BPOOL_FLUSH',
            N'SLEEP_DBSTARTUP',                 N'SLEEP_DCOMSTARTUP',
            N'SLEEP_MASTERDBREADY',             N'SLEEP_MASTERMDREADY',
            N'SLEEP_MASTERUPGRADED',            N'SLEEP_MSDBSTARTUP',
            N'SLEEP_SYSTEMTASK',                N'SLEEP_TASK',
            N'SLEEP_TEMPDBSTARTUP',             N'SNI_HTTP_ACCEPT',
            N'SP_SERVER_DIAGNOSTICS_SLEEP',     N'SQLTRACE_BUFFER_FLUSH',
            N'SQLTRACE_INCREMENTAL_FLUSH_SLEEP',
            N'SQLTRACE_WAIT_ENTRIES',           N'WAIT_FOR_RESULTS',
            N'WAITFOR',                         N'WAITFOR_TASKSHUTDOWN',
            N'WAIT_XTP_HOST_WAIT',              N'WAIT_XTP_OFFLINE_CKPT_NEW_LOG',
            N'WAIT_XTP_CKPT_CLOSE',             N'XE_DISPATCHER_JOIN',
            N'XE_DISPATCHER_WAIT',              N'XE_TIMER_EVENT',
            'PREEMPTIVE_OS_WRITEFILE')
        AND [waiting_tasks_count] > 0
     )
    INSERT INTO @SQLWaits
    SELECT
        MAX ([W1].[wait_type]) AS [wait_type],
        CAST (MAX ([W1].[wait_s]) AS DECIMAL (16,2)) AS [wait_s],
        CAST (MAX ([W1].[resource_s]) AS DECIMAL (16,2)) AS [resource_s],
        CAST (MAX ([W1].[signal_s]) AS DECIMAL (16,2)) AS [signal_s],
        MAX ([W1].[wait_count]) AS [wait_count],
        CAST (MAX ([W1].[percentage]) AS DECIMAL (5,2)) AS [percentage],
        CAST ((MAX ([W1].[wait_s]) / MAX ([W1].[wait_count])) AS DECIMAL (16,4)) AS [avg_wait_s],
        CAST ((MAX ([W1].[resource_s]) / MAX ([W1].[wait_count])) AS DECIMAL (16,4)) AS [avg_res_s],
        CAST ((MAX ([W1].[signal_s]) / MAX ([W1].[wait_count])) AS DECIMAL (16,4)) AS [avg_sig_s],
        CONVERT(SMALLDATETIME,CAST(@TimeRun AS VARCHAR)) AS [time_run]
    FROM [Waits] AS [W1]
    INNER JOIN [Waits] AS [W2]
        ON [W2].[row_num] <= [W1].[row_num]
    GROUP BY [W1].[row_num]
    HAVING SUM ([W2].[percentage]) - MAX ([W1].[percentage]) < 95; -- percentage threshold

    --------------------------------------------------------------------
    -- set step complete for all databases (runs once per instance)
    --------------------------------------------------------------------
    UPDATE @DBList SET step_complete = 7

    --------------------------------------------------------------------
    -- return collected data
    --------------------------------------------------------------------
    SELECT [wait_type], [wait_s], [resource_s], [signal_s], [wait_count], [percentage], [avg_wait_s], [avg_res_s], [avg_sig_s], [time_run] FROM @SQLWaits
END