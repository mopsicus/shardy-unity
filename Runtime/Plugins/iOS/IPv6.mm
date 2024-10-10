#include <sys/socket.h>
#include <netdb.h>
#include <arpa/inet.h>
#include <err.h>

#define MakeStringCopy(x) (x != NULL && [x isKindOfClass:[NSString class]]) ? strdup([x UTF8String]) : NULL

extern "C" {

const char *CheckIPv6(const char *host) {
    if (host == nil) {
        return NULL;
    }
    const char *data = "No";
    int n;
    struct addrinfo *res;
    struct addrinfo *res0;
    struct addrinfo hints;
    memset(&hints, 0, sizeof(hints));
    hints.ai_flags = AI_DEFAULT;
    hints.ai_family = PF_UNSPEC;
    hints.ai_socktype = SOCK_STREAM;
    if ((n = getaddrinfo(host, "http", &hints, &res0)) != 0) {
        return NULL;
    }
    struct sockaddr_in6 *addr6;
    NSString *ipv6 = NULL;
    char ipbuf[32];
    for (res = res0; res; res = res->ai_next) {
        if (res->ai_family == AF_INET6) {
            addr6 = (struct sockaddr_in6 *) res->ai_addr;
            data = inet_ntop(AF_INET6, &addr6->sin6_addr, ipbuf, sizeof(ipbuf));
            NSString *temp = [[NSString alloc] initWithCString:(const char *) data encoding:NSASCIIStringEncoding];
            ipv6 = temp;
        } else {
            ipv6 = NULL;
        }
        break;
    }
    freeaddrinfo(res0);
    NSString *result = ipv6;
    return MakeStringCopy(result);
}

}
