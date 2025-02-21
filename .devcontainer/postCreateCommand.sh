sudo apt update

sudo DEBIAN_FRONTEND=noninteractive \
    UCF_FORCE_CONFFOLD=1 \
    UCF_FORCE_CONFDEF=1 \
    apt -o Dpkg::Options::="--force-confdef" \
    -o Dpkg::Options::="--force-confold" \
    install openssh-server -y

sudo apt upgrade -y
sudo apt install xdg-utils -y