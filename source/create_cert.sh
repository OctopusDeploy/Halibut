#!/bin/bash
set -e

usage() { echo "Usage: create_cert.sh -c CN -o filename.pfx"; exit 1; }
while getopts ":c:o:" options; do
  case ${options} in
    c )
        cn=${OPTARG}
        ;;
    o )
        output=${OPTARG} 
        ;;
    * ) usage 
        ;;
  esac
done
shift $((OPTIND-1))

if [ -z "${cn}" ] || [ -z "${output}" ]; then
    usage
fi

echo "Creating a self signed certificate CN=${cn} filename: ${output}"

pwd=$(head /dev/urandom | tr -dc A-Za-z0-9 | head -c 13 ; echo '')
# create a self signed cert
openssl req -new -x509 -newkey rsa:2048 -keyout $output.key -out $output.cer -days 365 -subj /CN=$cn -passout pass:$pwd &>/dev/null

# export a PFX (set the export password to blank)
openssl pkcs12 -export -out $output -inkey $output.key -in $output.cer -passin pass:$pwd -passout pass:'' &>/dev/null
echo "Done!"
# Get a certificate thumbprint
openssl x509 -in $output.cer -noout -sha1 -fingerprint | sed -r 's/://g'

#cleanup
rm $output.cer $output.key