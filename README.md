## UPnP Port Opener

A simple command line port opener for UPnP-enabled routers

### Usage

`uppo PORT "SHORT DESCRIPTION"`  

### Operation

Tries to add a port-mapping on all local routers (devices with a WANIPConnection service) from
the port `PORT` on the router to the port `PORT` on the computer which runs it.  
It's a bit crappy right now and you cannot have different source and target ports, also you cannot
specify origin IP.

### Todo

 * Add more command line options.
 * Clean up code.
 * Only accept devices with WANIPConnection from the start rather than filtering them later.
 * Simple GUI?
 
### Dependencies

Uses the ManagedUPnP library from [managedupnp.codeplex.com](http://managedupnp.codeplex.com/).