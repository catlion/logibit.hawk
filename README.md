# Logibit Hawk

A F# implementation of the Hawk authentication protocol. Few dependencies. No
cruft. No thrown exceptions.

If this library throws an exception, report an issue - instead it uses return
values that are structured instead.

``` bash
paket add nuget Hawk
paket add nuget Hawk.Suave
```

Dependencies: { Aether, FSharp.Core, NodaTime }, nugets [Hawk][ng-h] and
[Hawk.Suave][ng-hs].

For all API methods implemented, the full test suite for those methods has also
been translated.

## Usage (Suave Example)

``` fsharp
open logibit.hawk
open logibit.hawk.Types
open logibit.hawk.Server

open Suave
open Suave.Http // houses submodule 'Hawk'
open Suave.Http.Successful
open Suave.Http.RequestErrors
open Suave.Types

// your own user type
type User =
  { homepage  : Uri
    realName : string }

// this is the structure that is the 'context' for logibit.hawk
let settings =
  // this is what the lib is looking for to verify the request
  let sampleCreds =
    { id        = "haf"
      key       = "werxhqb98rpaxn39848xrunpaw3489ruxnpa98w4rxn"
      algorithm = SHA256 }

  // the generic type param allows you to implement a generic user repository
  // for your own user type (above)
  { Settings.empty<User>() with
     // sign: UserId -> Choice<Credentials * 'a, CredsError>
     credsRepo = fun id ->
       (sampleCreds,
        { homepage = Uri("https://logibit.se"); realName = "Henrik" }
       )
       // no error:
       |> Choice1Of2 }

// you can compose this into the rest of the app, as it's a web part
let sampleApp settings : WebPart =
  Hawk.authenticate
    settings
    Hawk.bindReq
    // in here you can put your authenticated web parts
    (fun (attr, creds, user) -> OK (sprintf "authenticated user '%s'" user.realName))
    // on failure to authenticate the request
    (fun err -> UNAUTHORIZED (err.ToString()))
```

Currently the code is only fully documented - but not outside the code, so have
a browse to [the source
code](https://github.com/logibit/logibit.hawk/blob/master/src/logibit.hawk/Server.fs#L1)
that you are interested in to see how the API composes.

## Usage from client:

Use the .js file from `src/vendor/hawk.js/lib`, then you can wrap your ajax
calls like this:

**request.js:** (using CommonJS module layout, which you can use to require it and
get a function in return).

``` javascript
var Auth   = require('./auth.js'),
    Hawk   = require('./lib/hawk.js'),
    Logger = require('./logger.js'),
    jQuery = require('jquery');

var qt = function(str) {
  return "'" + str + "'";
}

var jqSetHawkHeader = function(opts, creds, jqXHR, settings) {
  if (typeof opts.contentType == 'undefined') {
    throw new Error('missing contentType from options');
  }

  var opts = jQuery.extend({ credentials: creds, payload: settings.data }, opts),
      // header(uri, method, options): should have options values for
      // - contentType
      // - credentials
      // - payload
      header = Hawk.client.header(settings.url, settings.type, opts); // type = HTTP-method

  if (typeof header.err !== 'undefined') {
    Logger.error('(1/2) Hawk error:', qt(header.err), 'for', method, qt(settings.url));
    Logger.error('(2/2) Using credentials', opts.credentials);
    return;
  }

  Logger.debug('(1/3)', settings.type, settings.url);
  Logger.debug('(2/3) opts:', opts);
  Logger.debug('(3/3) header:', header.field);

  jqXHR.setRequestHeader('Authorization', header.field);
};

module.exports = function (method, resource, data, opts) {
  var origin    = window.location.origin,
      creds     = Auth.getCredentials(),
      url       = origin + resource,
      opts      = jQuery.extend({
        contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
        dataType: 'html'
      }, (typeof opts !== 'undefined' ? opts : {})),
      jqOpts    = jQuery.extend({
        type:       method,
        data:       data,
        url:        url,
        beforeSend: function(xhr, s) { jqSetHawkHeader(opts, creds, xhr, s) }
      }, opts);

  return jQuery.ajax(jqOpts);
};
```

## Changelog

Please have a look at [Releases](https://github.com/logibit/logibit.hawk/releases).

## API

This is the public API of the library. It mimics the API of Hawk.js - the
[reference implementation](https://github.com/hueniverse/hawk/blob/master/lib/client.js).

### `logibit.hawk.Bewit`

These functions are available to creating and verifying Bewits.

 - [ ] generate - generate a new bewit from credentials, a uri and an optional
       ext field.
 - [ ] generate' - generate a new bewit from credentials, a string uri and an
       optional ext field.
 - [ ] authenticate - verify a given bewit

#### `authenticate` details

TBD - docs.

### `logibit.hawk.Client`

These functions are available, checked functions are implemented

 - [x] header - generate a request header for server to authenticate
 - [x] bewit - delegates to Bewit.generate
 - [ ] authenticate - test that server response is authentic, see
   [Response Payload Validation](https://github.com/hueniverse/hawk#response-payload-validation).
 - [ ] message - generate an authorisation string for a message

### `logibit.hawk.Server`

 - [x] authenticate - authenticate a request
 - [x] authenticatePayload - authenticate the payload of a request - assumes
   you first have called `authenticate` to get credentials.
   [Payload Validation](https://github.com/hueniverse/hawk#payload-validation)
 - [ ] authenticatePayloadHash
 - [ ] header - generate a server-header for the client to authenticate
 - [ ] authenticateBewit - authenticate a client-supplied bewit, see [Bewit
   Usage Example](https://github.com/hueniverse/hawk#bewit-usage-example).
   TODO: delegate to Bewit.authenticate
 - [ ] authenticateMessage - authenticate a client-supplied message

#### `authenticate` details

How strictly does the server validate its input? Compared to reference
implementation. This part is important since it will make or break the usability
of your api/app. Just throwing SecurityException for any of these is not
granular enough.

 - [x] server cannot parse header -> `FaultyAuthorizationHeader`
 - [x] server cannot find Hawk scheme in header -> `FaultyAuthorizationHeader`
 - [x] id, ts, nonce and mac (required attrs) are supplied -> `MissingAttribute`
 - [x] credential function errors -> `CredsError`
 - [x] mac doesn't match payload -> `BadMac`
 - [x] missing payload hash if payload -> `MissingAttribute`
 - [x] payload hash not matching -> `BadPayloadHash of hash_given * hash_calculated`
 - [x] nonce reused -> `NonceError AlreadySeen`, with in-memory cache
 - [x] stale timestamp -> `StaleTimestamp`

##### Hints when not under attack (in dev)

If you see `CredsError`, it's most likely a problem that you can't find the user
with your repository function.

If you see `BadMac`, it means probably means you haven't fed the right parameters
to `authenticate`. Log the input parameters, verify that host and port match
(are you behind a reverse proxy?) and check that the length of the content is
the same on the client as on the server.

The `BadMac` error comes from hashing a normalised string of these parameters:

 - hawk header version
 - type of normalisation ('header' in this case)
 - timestamp
 - nonce
 - method
 - resource (aka PathAndQuery for the constructed Uri)
 - host
 - port
 - hash value
 - ext if there is one
 - app if there is one
 - dlg if there is app and if there is one

If you see PadPayloadHash, it means that the MAC check passed, so you're
probably looking at an empty byte array, or your Content-Type isn't being passed
to the server properly, or the server implementation doesn't feed the correct
Content-Type header (e.g. it doesn't trim the stuff after the first MimeType
declaration, before the semi-colon `;`).

### `logibit.hawk.Crypto`

The crypto module contains functions for validating the pieces of the request.

 - [x] genNormStr - generate a normalised string for a request/auth data
 - [x] calcPayloadHash - calculates the payload hash from a given byte[]
 - [x] calcPayloadHash - calculates the payload hash from a given string
 - [x] calcHmac - calculates the HMAC for a given string

### `logibit.hawk.Types`

This module contains the shared types that you should use for interacting with
the above modules.

 - HttpMethod - discriminated union type of HTTP methods
 - Algo - The supported hash algorithms
 - Credentials - The credentials object used in both client and server
 - HawkAttributes - Recognised attributes in the Hawk header
 - FullAuth - A structure that represents the fully calculated hawk request data
   structure

This module also contains a module-per-type with lenses for that type. The
lenses follow the same format as [Aether](https://github.com/xyncro/aether)
recommends.

### `logibit.hawk.Logging`

Types:

 - `LogLevel` - the level of the LogLine.
 - `LogLine` - this is the data structure of the logging module, this is where
   you feed your data.
 - `Logger interface` - the main interface that we can log to/into.
 - `Logger module` - a module that contains functions equiv. to the instance
   methods of the logger interface.
 - `NoopLogger : Logger`  - the default logger, you have to replace it yourself

It's good to know that you have to construct your LogLine yourself. That
LogLines with Verbose or Debug levels should be sent to the `debug` or `verbose`
functions/methods of the module/interface Logger, which in turn takes functions,
which are evaluated if it's the case that the logging infrastructure is indeed
logging at that level.

This means that logging at that level, and computing the log lines, needs only
be done if we can really do something with them.

### `logibit.hawk.Choice`

This module adds some functions for composing Choice-s:

 - `ofOption : onError:'b -> Choice<'a, 'b>` - convert an option to a choice
 - `(>>=) : m:Choice<'a, 'b> -> f:('a -> Choice<'c, 'b>) -> Choice<'c, 'b>` -
   the normal bind operator, defined on choice
 - `bind` - same as above
 - `(>>!) : m:Choice<'a, 'b> -> f:('b -> 'c) -> Choice<'a, 'c>` - the normal
   bind operator, defined on the error case of choice
 - `bind2` - same as above
 - `lift : a:'a -> Choice<'a, 'b>` - lift the value a into the choice
 - `(>>~) : a:'a -> f:('a -> Choice<'c, 'b>) -> Choice<'c, 'b>` - lift the value
   a and bind f to the resulting choice -- useful for "start with this value and
   then run this sequence of bind/map/map2 on the choice values that flow".
 - `liftBind` - same as above
 - `(>>-) : m:Choice<'a, 'b> -> f:('a -> 'c) -> Choice<'c, 'b>` - map the
   first/successful choice value to another one (and another type, possibly).
 - `map` - same as above
 - `(>>@) : m:Choice<'a, 'b> -> f:('b -> 'c) -> Choice<'a, 'c>` - map the
   second/error choice value to another one (and another type, possibly).
 - `map2` - same as above
 - `(>>*) : m:Choice<'a, 'b> -> f:('b -> unit) -> Choice<'a, 'b>` - inject
   a side-effect if the choice is in the error case.
 - `inject2` - same as above

#### Example

From the source code, with annotations:

``` fsharp
let validateNonce validator
                  ((attrs : HawkAttributes), cs) :
                  : Choice<_, AuthError> =
  validator (attrs.nonce, attrs.ts) // => Choice<unit, NonceError>
  >>- fun _ -> attrs, cs // => Choice<HawkAttributes * 'cs, NonceError>
  >>@ AuthError.fromNonceError
  // => Choice<HawkAttributes * 'cs, AuthError>
```

### Other APIs

There are some modules that are currently internal as to avoid conflicting with
existing code. If these are made 'more coherent' or else moved to external
libraries, they can be placed on their own and be made public. The modules like this are `Random`, `Prelude`, `Parse`.

[ng-h]: https://www.nuget.org/packages/Hawk/
[ng-hs]: https://www.nuget.org/packages/Hawk.Suave/
