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
US9: User might have already lots of flights done, it shall be possible to import a logbook file (either in json or xml format) created by another application
US9.1: When importing logbook, it shall be checked that no duplicates exists
US10: User shall be able to investigate the logbook in text format by setting different kind of filters, se US6.1 - US6.5. This investigation shall be in another tab from the map
US11: The airports found from the logbook shall be also indicated in map. The filters that can be used to investigate the logbook, shall also be available in map view
US12: The application shall use appData/Local/DestinationPlanner folder to persistently store configuration, logbook and necessary airport information files. The folder shall be created if it doesn't exist when the app is started
US13: When user ask app to load airport data, the files shall be copied to appdata folder and shall be used from there by default. If user wants to update the airport files, user will again ask the app to load airport data and app fill copy the files to appdata folder and overwrite the existing airport files
US14: The logbook shall be written to appdata folder. Flight information shall be automatically stored to the file, user doesn't have to remember to save it. If no logbook exist in appdata folder, application shall create a default empty logbook. The default logbook name shall be logbook-<dd>-<mm>-<yyyy>.xml (current date shall be used in filename).
US15: User shall have an option to import and export logbook in format known to application.
US15.1: When importing, a new logbook shall be created. The filename format shall follow the same rules as with default logbook. If file already exists, a running number shall be inserted between the date and word "logbook"
US15.2: When exporting, user shall be given an option to select where the logbook shall be written and possibly change the logbook file name
US15.3: When there are more than one logbook file in appdata folder, application shall ask which one to use when starting up. If only one logbook file exists, app will use that automatically
US16: When clicking an airport from a map, a popup window shall appear anchored near the airport marker. The popup shall show:
US16.1: The ICAO code and name of the airport
US16.2: Each individual runway with its length in feet (sorted longest first). If no runway data is available, "N/A" is shown.
US16.3: The current METAR fetched from an external source (aviationweather.gov). While loading, "METAR: Loading…" is shown. If no METAR is available, "METAR: Not available" is shown.
US17: It shall be possible to select two airports simultaneously.
US17.1: Clicking the left mouse button on an airport opens a single primary popup (blue border). Any previously selected secondary airport is cleared.
US17.2: Clicking Ctrl+left mouse button on an airport opens a secondary popup (orange border) alongside the primary. If no primary is selected yet, the clicked airport becomes the primary.
US17.3: Clicking Ctrl+left mouse again replaces the secondary airport with the newly clicked one.
US17.4: When two airports are selected, a dashed line is drawn between them on the map with the distance in nautical miles shown at the midpoint.
US17.5: Both popups are anchored near their respective airport markers and move with the map when the user pans or zooms.
US17.6: Clicking an empty area on the map closes both popups and clears the selection.
US17.7: [DONE] Popups should follow the the window focus. If program is minimized, popup should follow. Also if another window is switched on top of this app, popups should not stay on top 
US17.8: [DONE] If the main app window is moved, popups should follow and be anchored to airport
US18: [DONE] I want to user to have a possibility to use search in map screen
US18.1: [DONE] User could use ICAO code or airport name as search key
US18.2: [DONE] Once user starts to enter search key, a list of airports found so far has been shown. List shall be updated after every key entered
US18.3: [DONE] User will select the airport from the drop down list
US18.4: [DONE] Once airport is selected, map will center and zoom in for the selected airport