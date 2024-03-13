// Purpose: Header file for the TLScope class.
#ifndef _TS_HEADER_4204
#define _TS_HEADER_4204

#include <iostream>
#include <string>
#include <memory>
#include <map>

// forward declarations
class USER;

// TLScope class
class TLScope {
public:
    TLScope();
    TLScope(const std::string &name);

    void run();
private:
    std::shared_ptr<USER> user;
    std::map<std::string, std::shared_ptr<USER>> registered_users;
    std::map<std::string, std::shared_ptr<USER>> online_users;
};

#endif // _TS_HEADER_04022004
