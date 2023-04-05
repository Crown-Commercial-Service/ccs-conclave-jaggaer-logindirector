Login Director
===========

Overview
--------
This is the code for the Crown Commercial Services (_CCS_) 
Login Director application, which serves to link the Jaegger and CAS systems with PPG and provide a mechanism to authenticate and register users within the former from the latter.  Effectively, the application serves to make API requests from and redirect users between the three applications as its only purpose.

Structure
---------
The solution is implemented using .NET Core (C#) and consists of the following projects:

* logindirector (core application)
* LoginDirectorTests (limited unit test library)

Technology Overview
-------------------
The core technologies for the project are:

* .NET Core (C#) v6
* .NET OAuth middleware
* Razor templating
* MSTest Test Framework
* AWS for storing secrets
* CCS-Frontend-Kit solution

Building and Running Locally
----------------------------
In order to run the application locally you will first require either a local secrets file (can be supplied by the lead developer), or to connect your local machine to an authorised AWS account (preferable solution).

Once this is in place, simply use Visual Studio  to run the logindirector project to launch the application. 

Unit tests can be run by using Visual Studio to 'Run All Tests' within the LoginDirectorTests project.

Copyright
---------
Copyright (c) Crown CommercialService 2022.

This source code is licensed under the Open Government Licence 3.0.

http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/
