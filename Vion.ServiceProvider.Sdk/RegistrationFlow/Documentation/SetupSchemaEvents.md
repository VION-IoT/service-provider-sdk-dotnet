Initial draft of setup-schema event shapes exchanged between Mesh and service providers.

```
// SetupSchemaEvent (here's what CAN be configured, Flat Form)
// topic: {installationTopic}/setup/req/+
// response topic: no
// headers: ?
// retain: no
{
  "fields": [
    // --- Extension Boards ---
    { "identifier": "has_rs485",  "name": "RS485 + I2C Extension Board",  "group": "Extension Boards",  "type": "bool",  "default": false },
    { "identifier": "baudrate",   "name": "RS485 Baudrate",               "group": "Extension Boards",  "type": "enum",  "options": { "values": [9600, 19200, 38400, 115200] },  "default": 9600,
      "visibleWhen": { "fieldId": "has_rs485", "equals": true }
    },
    { "identifier": "parity",     "name": "RS485 Parity",                 "group": "Extension Boards",  "type": "enum",  "options": { "values": ["None", "Even", "Odd"] },       "default": "None",
      "visibleWhen": { "fieldId": "has_rs485", "equals": true }
    },
    { "identifier": "has_relay",  "name": "4-Channel Relay Board",        "group": "Extension Boards",  "type": "bool",  "default": false },

    // --- GPIO Pin Configuration ---
    // One field per usable GPIO pin. When an extension board claims a pin, the HAL
    // validates on receive (not prevented by the UI).
    { "identifier": "gpio2",      "name": "GPIO 2 (SDA1)",                "group": "GPIO Pins",  "type": "enum",  "options": { "values": ["Unused", "DigitalInput", "DigitalOutput"] },  "default": "Unused" },
    { "identifier": "gpio3",      "name": "GPIO 3 (SCL1)",                "group": "GPIO Pins",  "type": "enum",  "options": { "values": ["Unused", "DigitalInput", "DigitalOutput"] },  "default": "Unused" },
    { "identifier": "gpio4",      "name": "GPIO 4",                       "group": "GPIO Pins",  "type": "enum",  "options": { "values": ["Unused", "DigitalInput", "DigitalOutput"] },  "default": "Unused" },
    { "identifier": "gpio5",      "name": "GPIO 5",                       "group": "GPIO Pins",  "type": "enum",  "options": { "values": ["Unused", "DigitalInput", "DigitalOutput"] },  "default": "Unused" },
    { "Iidentifierd": "gpio14",   "name": "GPIO 14 (TXD)",                "group": "GPIO Pins",  "type": "enum",  "options": { "values": ["Unused", "DigitalInput", "DigitalOutput"] },  "default": "Unused" },
    { "identifier": "gpio15",     "name": "GPIO 15 (RXD)",                "group": "GPIO Pins",  "type": "enum",  "options": { "values": ["Unused", "DigitalInput", "DigitalOutput"] },  "default": "Unused" },
    // ... one field per usable GPIO

    // --- Per-pin parameters (visible only when pin is configured as DigitalInput) ---
    { "identifier": "gpio4_pull",  "name": "GPIO 4 — Pull Resistor",     "group": "GPIO Pins",  "type": "enum",  "options": { "values": ["None", "PullUp", "PullDown"] },  "default": "None",
      "visibleWhen": { "fieldId": "gpio4", "equals": "DigitalInput" }
    },
    { "identifier": "gpio4_edge",  "name": "GPIO 4 — Edge Detection",    "group": "GPIO Pins",  "type": "enum",  "options": { "values": ["None", "Rising", "Falling", "Both"] },  "default": "None",
      "visibleWhen": { "fieldId": "gpio4", "equals": "DigitalInput" }
    }
    // ... repeat per pin that supports DigitalInput parameters
  ]
}


// SetupSelectionEvent (here's what the integrator CHOSE, just a flat key-value map)
// topic: {installationTopic}/setup/set/{spId}
// response topic: no
// headers: ?
// retain: no
{
  "Values": {
    "has_rs485": true,
    "baudrate": 9600,
    "parity": "None",
    "has_relay": false,
    "gpio2": "Unused",
    "gpio3": "Unused",
    "gpio4": "DigitalInput",
    "gpio4_pull": "PullUp",
    "gpio4_edge": "Rising",
    "gpio5": "DigitalOutput",
    "gpio14": "Unused",
    "gpio15": "Unused"
  }
}


// SetupAcknowledge (todo: syncstate?)
// topic: {installationTopic}/setup/ack/{spId}
{
  "success": true,
  "errorMessage": ""
}



``` 