# ECommerceApp
> Simple Ecommerce application that allows place an offer on the website and order items.

# Admin User
> login:     admin@localhost
> password:  aDminN@W25!

# Problems with start
1. Update database using command Update-Database (apply all migration)
2. Add account google credentials, first navigate console to ECommerceApp.Web directory then
use this command or you can change it in appsettings.json section Authentication and Google
* dotnet user-secrets set "Authentication:Google:ClientId" "ClientIdFromGoogle"
* dotnet user-secrets set "Authentication:Google:ClientSecret" "ClientSecretFromGoogle"

Where can i get ClientId and ClientSecret?
* https://console.cloud.google.com/
Credentials and choose OAuth2, then create new data logging. In the section URI place https://localhost:44364/signin-google

# Docker run
Before run docker setup your database 
https://stackoverflow.com/questions/66210339/how-to-connect-to-a-local-sql-server-express-database-from-a-docker-composed-c-s 
Then add user docker to SQL using this script:
``` sql
CREATE LOGIN docker WITH PASSWORD=N'docker', 
                 DEFAULT_DATABASE=[master], CHECK_EXPIRATION=OFF, CHECK_POLICY=OFF

EXEC sp_addsrvrolemember 'docker', 'sysadmin'
CREATE USER docker FOR LOGIN docker WITH DEFAULT_SCHEMA=[dbo]
```
Make sure that option "SQL Server and Windows Authentication mode" is configured in MSSQL. 
Next in ECommerceApp root directory in powershell or cmd run this command:
> Docker compose up

## Technologies
* .NET 7
* ASP.NET, HTML5, CSS3, JS, MSSQL
* WebAPI
* Depedency Injection
* Entity Framework Core 
* LINQ
* Fluent Validation 
* AutoMapper 
* XUnit
* Moq 
* Fluent Assertions 
* jQuery
* Bootstrap
* RequireJS

## General info
ECommerceApp is a web application written using MVC pattern. Application is made using clean and onion architecture. This application also includes a simple login system.

## Project
Application divided into repositories and services. 
Database scheme is shown on the figure below:

![Database scheme](schemat_bazy.png)

## Screens
Screen 1
![screen_1](screen_1.PNG)

Screen 2
![screen_2](screen_2.PNG)

Screen 3
![screen_3](screen_3.PNG)

Screen 4
![screen_4](screen_4.PNG)

Screen 5
![screen_5](screen_5.PNG)

Screen 6
![screen_6](screen_6.PNG)

Screen 7
![screen_7](screen_7.PNG)

Screen 8
![screen_8](screen_8.PNG)

Screen 9
![screen_9](screen_9.PNG)

Screen 10
![screen_10](screen_10.PNG)

Screen 11
![screen_11](screen_11.PNG)

Screen 12
![screen_12](screen_12.PNG)

Screen 13
![screen_13](screen_13.PNG)

Screen 14
![screen_14](screen_14.PNG)

Screen 15
![screen_15](screen_15.PNG)

Screen 16
![screen_16](screen_16.PNG)

Screen 17
![screen_17](screen_17.PNG)

Screen 18
![screen_18](screen_18.PNG)

Screen 19
![screen_19](screen_19.PNG)

Screen 20
![screen_20](screen_20.PNG)

Screen 21
![screen_21](screen_21.PNG)

Screen 22
![screen_22](screen_22.PNG)

Screen 23
![screen_23](screen_23.PNG)

Screen 24
![screen_24](screen_24.PNG)

Screen 25
![screen_25](screen_25.PNG)

Screen 26
![screen_26](screen_26.PNG)

Screen 27
![screen_27](screen_27.PNG)

Screen 28
![screen_28](screen_28.PNG)

Screen 29
![screen_29](screen_29.PNG)

Screen 30
![screen_30](screen_30.PNG)

Screen 31
![screen_31](screen_31.PNG)

Screen 32
![screen_32](screen_32.PNG)

Screen 33
![screen_33](screen_33.PNG)

Screen 34
![screen_34](screen_34.PNG)

Screen 35
![screen_35](screen_35.PNG)

Screen 36
![screen_36](screen_36.PNG)

## Status
Project is finished, but not closed. In the future, it is possible to expand the application in additional functionalities.
