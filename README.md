NuGet Gallery For Remote Feeds
=======================================================================
This is a fork of standard [NuGetGallery](https://github.com/NuGet/NuGetGallery) to host a read-only gallery with packages from configured remote feed.
For example, I'm using it for the public MyGet feed which is not listed in their gallery.

## Build and Run the Gallery in (arbitrary number) easy steps

1. Prerequisites. Install these if you don't already have them:
 1. Visual Studio 2013
 2. PowerShell 2.0 (comes with Windows 7+)
 3. [NuGet](http://docs.nuget.org/docs/start-here/installing-nuget)
 4. [Windows Azure SDK](http://www.microsoft.com/windowsazure/sdk/) - Note that you may have to manually upgrade the ".Cloud" projects in the solution if a different SDK version is used.
 5. (Optional, for unit tests) [xUnit for Visual Studio 2012 and 2013](http://visualstudiogallery.msdn.microsoft.com/463c5987-f82b-46c8-a97e-b1cde42b9099)
2. Clone it!
    
    ```git clone git@github.com:NuGet/NuGetGallery.git```
3. Build it!
    
    ```
    cd NuGetGallery
    .\build
    ```
4. Set up the website in IIS Express!
 1. We highly recommend using IIS Express. Use the [Web Platform Installer](http://microsoft.com/web) to install it if you don't have it already (it comes with recent versions of VS and WebMatrix though). Make sure to at least once run IIS Express as an administrator.
 2. In an ADMIN powershell prompt, run the `.\tools\Enable-LocalTestMe.ps1` file. It allows non-admins to host websites at: `http(s)://nuget.localtest.me`, it configures an IIS Express site at that URL and creates a self-signed SSL certificate. For more information on `localtest.me`, check out [readme.localtest.me](http://readme.localtest.me).
 3. If you're having trouble, go to the _Project Properties_ for the Website project, click on the _Web_ tab and change the URL to `localhost:port` where _port_ is some port number above 1024.
 4. When running the application using the Azure Compute emulator, you may have to edit the `.\src\NuGetGallery.Cloud\ServiceConfiguration.Local.cscfg` file and set the certificate thumbprint for the setting `SSLCertificate` to the certificate thumbprint of the generated `nuget.localtest.me` certificate from step 2. You can get a list of certificates and their thumbprints using PowerShell, running `Get-ChildItem -path cert:\LocalMachine\My`.

5. No database setup required.

6. Change the value of Gallery.ConfirmEmailAddresses to false in Web.Config file under src\NuGetGallery, this is required to upload the packages after registration.

7. Ensure the 'NuGetGallery' project (under the Frontend folder) is set to the Startup Project
  

That's it! You should now be able to press Ctrl-F5 to run the site!

## Copyright and License
Copyright 2015 .NET Foundation

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this work except in compliance with 
the License. You may obtain a copy of the License in the LICENSE file, or at:

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on 
an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the 
specific language governing permissions and limitations under the License.
