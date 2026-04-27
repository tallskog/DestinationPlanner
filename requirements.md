# Flight simulator destination planner
This is an application that can be used for planning the origin and destinations for your simulator flights. It is also capable of keeping track of your flights

## User stories
US1: Application shall be built using C#
US2: WPF shall be used as a UI framework
US3: Application shall have a map display that is possible to zoom in/out with mouse wheel, and drag around
US4: Map shall show (higlighted) airports in the visible area
US5: User is able to filter the airports
US5.1: Based on runway length (can set min and max length)
US5.1.1: User can select whether feets or meters is used when giving min and max runway length
US5.2: Based on instrument approach capabilites of the airport
US5.3: By first selecting certain airport and then giving a radius in nm
US6: Application shall be able to connect to msfs2024 and collect information from a flight with following information
US6.1: Date of flight
US6.2: Type of the aircraft (aircraft, helicopter)
US6.2: Time or releasing of the handbrake
US6.3: Departure airport
US6.4: Time of setting the handbrake after arrival
US6.5: Arrival airport
US6.6: Tracking of the flight is not needed
US7: If depature and arrival are the same airport, the flight shall not be collected
US8: All flights shall be stored to an external file (it shall be decided whether json or xml format shall be used), which will work as flight logbook for this application
US9: User might have already lots of flights done, it shall be possible to import a logbook file (either in json or xmö format) created by another application
US9.1: When importing logbook, it shall be checked that no duplicates exists
US10: User shall be able to investigate the logbook in text format by setting different kind of filters, se US6.1 - US6.5. This investigation shall be in another tab from the map
US11: The airports found from the logbook shall be also indicated in map. The filters that can be used to investigate the logbook, shall also be available in map view