# ECommerceApp
> Simple Ecommerce application that allows place an offer on the website and order items.

## Technologies
* .NET Core 3.1
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
* Bootstrap

## General info
ECommerceApp is a web application written using MVC pattern. Application is made using clean and onion architecture. This application also includes a simple login system.

## Project
Application divided into repositories and services. The main purpose of this split was not to use entities directly. Services used ViewModels to modify, add, delete values to the database. Service had to map ViewModel into Entity. After that entity was sent to repositories, which modify data in the database. If the data was provided from database, service had to map Entity into ViewModel. 
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

## Status
Project is finished, but not closed. In the future, it is possible to expand the application in additional functionalities.
